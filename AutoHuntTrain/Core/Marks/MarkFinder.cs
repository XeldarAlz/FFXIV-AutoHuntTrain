using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Numerics;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CSObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;
using StatusFlags = Dalamud.Game.ClientState.Objects.Enums.StatusFlags;

namespace AutoHuntTrain.Core.Marks;

internal readonly record struct MarkSighting(
    ulong GameObjectId,
    int ObjectIndex,
    Vector3 Position,
    float HitboxRadius,
    float DistanceToHitbox,
    uint CurrentHp);

// A hunt mark of the zone seen near a flag. Pulled is false while it stands at full health with nobody fighting it.
internal readonly record struct ZoneMarkSighting(uint NameId, HuntMarkRank Rank, bool Pulled, MarkSighting Sighting);

// Walks the object table through its cached wrappers, so a scan allocates nothing and is cheap enough for a move's stop check.
internal static unsafe class MarkFinder
{
    // The id the game stores when a character targets nothing.
    private const ulong NoTargetId = 0xE0000000;

    // Only mobs outside any FATE count: a FATE's mobs are never fought outside it.
    public static bool TryFindNearest(uint nameId, bool honorClaims, Vector3 from, ReadOnlySpan<ulong> ignored, out MarkSighting sighting, out int claimedSkipped)
    {
        sighting = default;
        claimedSkipped = 0;
        var found = false;
        var bestDistance = float.MaxValue;
        var objects = Svc.Objects;
        var localPlayerId = objects.LocalPlayer?.GameObjectId ?? 0;
        for (var objectIndex = 0; objectIndex < objects.Length; objectIndex++)
        {
            if (objects[objectIndex] is not IBattleNpc npc || npc.NameId != nameId)
            {
                continue;
            }

            if (!IsHuntable(npc) || ignored.Contains(npc.GameObjectId))
            {
                continue;
            }

            if (honorClaims && ClaimedByOther(npc.TargetObjectId, localPlayerId))
            {
                claimedSkipped++;
                continue;
            }

            var distance = DistanceToHitbox(from, npc);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            sighting = Describe(npc, objectIndex, distance);
            found = true;
        }

        return found;
    }

    // The nearest live A or S rank of the zone within radiusMeters of the flag, measured over the ground because a flag
    // has no height. The SS marks and their minions roam the whole expansion and are never a train's stop.
    public static bool TryFindNearestZoneMark(uint territoryId, Vector3 flag, float radiusMeters, Vector3 from, out ZoneMarkSighting sighting)
    {
        sighting = default;
        var found = false;
        var bestDistanceSquared = radiusMeters * radiusMeters;
        var objects = Svc.Objects;
        for (var objectIndex = 0; objectIndex < objects.Length; objectIndex++)
        {
            if (objects[objectIndex] is not IBattleNpc npc || !IsZoneTrainMark(npc.NameId, territoryId, out var rank) || !IsHuntable(npc))
            {
                continue;
            }

            var distanceSquared = GroundDistance.SquaredBetween(flag, npc.Position);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            sighting = new ZoneMarkSighting(npc.NameId, rank, IsPulled(npc), Describe(npc, objectIndex, DistanceToHitbox(from, npc)));
            found = true;
        }

        return found;
    }

    public static bool TryGetLive(ulong gameObjectId, Vector3 from, out MarkSighting sighting)
    {
        sighting = default;
        var objects = Svc.Objects;
        for (var objectIndex = 0; objectIndex < objects.Length; objectIndex++)
        {
            if (objects[objectIndex] is not IBattleNpc npc || npc.GameObjectId != gameObjectId)
            {
                continue;
            }

            if (!IsAlive(npc))
            {
                return false;
            }

            sighting = Describe(npc, objectIndex, DistanceToHitbox(from, npc));
            return true;
        }

        return false;
    }

    public static IGameObject? Resolve(in MarkSighting sighting)
    {
        var gameObject = Svc.Objects[sighting.ObjectIndex];
        return gameObject is not null && gameObject.GameObjectId == sighting.GameObjectId ? gameObject : null;
    }

    // The health of a sighted mark as a fraction of its maximum; false once the object is gone or its slot holds another.
    public static bool TryReadHealth(in MarkSighting sighting, out float fraction)
    {
        if (Resolve(sighting) is IBattleNpc { MaxHp: > 0 } npc)
        {
            fraction = npc.CurrentHp / (float)npc.MaxHp;
            return true;
        }

        fraction = 0f;
        return false;
    }

    private static bool IsZoneTrainMark(uint nameId, uint territoryId, out HuntMarkRank rank)
    {
        rank = default;
        var index = HuntMarkRegistry.IndexOf(nameId);
        if (index == HuntMarkRegistry.NotFound || HuntMarkRegistry.IsExpansionWideAt(index))
        {
            return false;
        }

        var mark = HuntMarkRegistry.Marks[index];
        rank = mark.Rank;
        return mark.TerritoryId == territoryId && rank is HuntMarkRank.A or HuntMarkRank.S;
    }

    // A mark nobody has touched stands at full health and out of combat.
    private static bool IsPulled(IBattleNpc npc)
        => npc.CurrentHp < npc.MaxHp || (npc.StatusFlags & StatusFlags.InCombat) != 0;

    private static bool IsHuntable(IBattleNpc npc)
    {
        if (!IsAlive(npc))
        {
            return false;
        }

        var native = (CSGameObject*)npc.Address;
        return native->BattleNpcSubKind == BattleNpcSubKind.Combatant && native->FateId == 0;
    }

    private static bool IsAlive(IBattleNpc npc) => npc.IsTargetable && !npc.IsDead && npc.CurrentHp > 0;

    // A mob another player's party pulled first gives that party the credit, so fighting it would never count.
    private static bool ClaimedByOther(ulong targetId, ulong localPlayerId)
    {
        if (targetId == 0 || targetId == NoTargetId || targetId == localPlayerId)
        {
            return false;
        }

        var manager = GameObjectManager.Instance();
        if (manager == null)
        {
            return false;
        }

        var target = manager->Objects.GetObjectByGameObjectId(targetId);
        if (target == null || target->ObjectKind != CSObjectKind.Pc)
        {
            return false;
        }

        var groups = GroupManager.Instance();
        return groups == null || !groups->MainGroup.IsEntityIdInParty(target->EntityId);
    }

    private static MarkSighting Describe(IBattleNpc npc, int objectIndex, float distance)
        => new(npc.GameObjectId, objectIndex, npc.Position, npc.HitboxRadius, distance, npc.CurrentHp);

    private static float DistanceToHitbox(Vector3 from, IBattleNpc npc)
        => MathF.Max(0f, Vector3.Distance(from, npc.Position) - npc.HitboxRadius);
}
