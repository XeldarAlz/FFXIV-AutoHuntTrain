using AutoHuntTrain.Core.Game.Ops;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
using System.Text;
using AddonSheet = Lumina.Excel.Sheets.Addon;

namespace AutoHuntTrain.Core.Game.Watchers;

// The game asks "Join X's party?" when a player invites the character. On a train the invite is taken, since party
// members share the credit on a mark, and only while a ride runs; any other invite is left for the player. The prompt
// is matched against its Addon sheet template, so the check works in every client language, and the addon is looked
// up again by id right before it is clicked.
internal sealed unsafe class PartyInviteWatcher : IDisposable
{
    private const ushort NoInvite = 0;
    private const string SelectYesnoAddonName = "SelectYesno";
    private const uint JoinPartyPromptRow = 120;
    // A prompt clicked the frame it opens can drop the click, so the accept waits a moment.
    private const int AcceptDelayMs = 500;
    private const int NotReadyAbandonMs = 15_000;

    private PromptTemplate joinPrompt = PromptTemplate.Invalid;
    private bool templateLoaded;

    private ushort inviteAddonId = NoInvite;
    private long acceptAtTick;
    private string inviterName = "";
    private string inviterWorld = "";

    public PartyInviteWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, SelectYesnoAddonName, OnSelectYesnoSetup);
        Svc.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, SelectYesnoAddonName, OnSelectYesnoSetup);
    }

    private static bool AcceptArmed()
    {
        var plugin = Plugin.Instance;
        return plugin.Configuration.AcceptPartyInvites
            && plugin.Controller.OpenToParty
            && !PartyOps.InParty();
    }

    private void OnSelectYesnoSetup(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon is null || inviteAddonId != NoInvite || !AcceptArmed())
        {
            return;
        }

        EnsureTemplate();
        var prompt = ReadPrompt(addon);
        if (!joinPrompt.Matches(prompt))
        {
            RunLog.Debug($"SelectYesno {addon->Id} is not a party invite: \"{prompt}\"");
            return;
        }

        CaptureInviter();
        inviteAddonId = addon->Id;
        acceptAtTick = Environment.TickCount64 + AcceptDelayMs;
        RunLog.Info($"Party invite from {DisplayName()} during the ride; accepting it.");
    }

    private void OnUpdate(IFramework _)
    {
        if (inviteAddonId == NoInvite)
        {
            return;
        }

        var addon = FindSelectYesno(inviteAddonId);
        if (addon is null)
        {
            RunLog.Debug("Party invite: the prompt closed before the accept; standing down.");
            inviteAddonId = NoInvite;
            return;
        }

        var now = Environment.TickCount64;
        if (now < acceptAtTick)
        {
            return;
        }

        if (!AcceptArmed())
        {
            RunLog.Info($"Party invite from {DisplayName()} left for you to answer: the ride is no longer open to a party.");
            inviteAddonId = NoInvite;
            return;
        }

        if (!GenericHelpers.IsAddonReady(addon))
        {
            if (now >= acceptAtTick + NotReadyAbandonMs)
            {
                RunLog.Warning($"Party invite: prompt {addon->Id} never became ready; standing down.");
                inviteAddonId = NoInvite;
            }

            return;
        }

        inviteAddonId = NoInvite;
        try
        {
            new AddonMaster.SelectYesno((nint)addon).Yes();
            RunLog.Info($"Accepted the party invite from {DisplayName()}.");
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Party invite: the accept click threw.");
        }
    }

    private static AtkUnitBase* FindSelectYesno(ushort addonId)
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonById(addonId);
        if (addon is null)
        {
            return null;
        }

        return addon->NameString == SelectYesnoAddonName ? addon : null;
    }

    private static string ReadPrompt(AtkUnitBase* addon)
    {
        try
        {
            var master = new AddonMaster.SelectYesno((nint)addon);
            var text = master.Addon->PromptText is not null ? master.Text : master.TextLegacy;
            return PromptTemplate.Normalize(text).Trim();
        }
        catch (Exception exception)
        {
            RunLog.Debug($"Party invite: failed to read the SelectYesno prompt: {exception.Message}");
            return "";
        }
    }

    private void EnsureTemplate()
    {
        if (templateLoaded)
        {
            return;
        }

        templateLoaded = true;
        try
        {
            joinPrompt = PromptTemplate.FromAddonRow(JoinPartyPromptRow);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Party invite: failed to read the Addon sheet prompt template.");
        }

        if (!joinPrompt.IsValid)
        {
            RunLog.Warning("Party invite: the prompt template is unavailable; invites cannot be identified.");
            return;
        }

        RunLog.Debug($"Party invite template: join '{joinPrompt}'.");
    }

    private void CaptureInviter()
    {
        inviterName = "";
        inviterWorld = "";
        try
        {
            var proxy = InfoProxyPartyInvite.Instance();
            if (proxy is null)
            {
                return;
            }

            inviterName = proxy->InviterName.ToString();
            var world = Svc.Data.GetExcelSheet<World>().GetRowOrDefault(proxy->InviterWorldId);
            inviterWorld = world?.Name.ExtractText() ?? "";
        }
        catch (Exception exception)
        {
            RunLog.Debug($"Party invite: failed to read the inviter: {exception.Message}");
        }
    }

    private string DisplayName()
        => string.IsNullOrEmpty(inviterName) ? "a player"
         : string.IsNullOrEmpty(inviterWorld) ? inviterName
         : $"{inviterName}@{inviterWorld}";

    // The Addon sheet text with the player-name placeholder cut out: the literal text before and after it. Layout-only
    // macros (line break, non-breaking space, soft hyphen) are dropped from both sides so they cannot break the match.
    private readonly struct PromptTemplate
    {
        public static readonly PromptTemplate Invalid = new("", "");

        private readonly string prefix;
        private readonly string suffix;

        private PromptTemplate(string prefix, string suffix)
        {
            this.prefix = prefix;
            this.suffix = suffix;
        }

        public bool IsValid => prefix.Length + suffix.Length > 0;

        public bool Matches(string prompt)
            => IsValid
            && prompt.Length > prefix.Length + suffix.Length
            && prompt.StartsWith(prefix, StringComparison.Ordinal)
            && prompt.EndsWith(suffix, StringComparison.Ordinal);

        public override string ToString() => $"{prefix}<name>{suffix}";

        public static PromptTemplate FromAddonRow(uint rowId)
        {
            var row = Svc.Data.GetExcelSheet<AddonSheet>().GetRowOrDefault(rowId);
            if (row is null)
            {
                return Invalid;
            }

            var prefix = new StringBuilder();
            var suffix = new StringBuilder();
            var sawPlaceholder = false;
            foreach (var payload in row.Value.Text)
            {
                if (payload.Type == ReadOnlySePayloadType.Text)
                {
                    (sawPlaceholder ? suffix : prefix).Append(Encoding.UTF8.GetString(payload.Body.Span));
                    continue;
                }

                if (IsLayoutMacro(payload))
                {
                    continue;
                }

                sawPlaceholder = true;
                suffix.Clear();
            }

            return sawPlaceholder
                ? new PromptTemplate(Normalize(prefix.ToString()), Normalize(suffix.ToString()))
                : Invalid;
        }

        public static string Normalize(string text)
        {
            var builder = new StringBuilder(text.Length);
            for (var charIndex = 0; charIndex < text.Length; charIndex++)
            {
                var character = text[charIndex];
                if (character is '\r' or '\n' or '\u00A0' or '\u00AD')
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private static bool IsLayoutMacro(in ReadOnlySePayload payload)
            => payload.Type == ReadOnlySePayloadType.Macro
            && payload.MacroCode is MacroCode.NewLine or MacroCode.NonBreakingSpace or MacroCode.SoftHyphen;
    }
}
