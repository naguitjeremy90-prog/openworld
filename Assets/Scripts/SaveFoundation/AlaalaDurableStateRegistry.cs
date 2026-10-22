using System;
using System.Collections.Generic;
using System.Linq;

namespace Alaala.Saving
{
    public sealed class AlaalaStateRule
    {
        public readonly SaveValueKind Kind;
        public readonly SaveStateClass Classification;
        public readonly int MaximumInteger;
        public bool Durable => Classification <= SaveStateClass.CompletedPresentation;
        internal AlaalaStateRule(SaveValueKind kind, SaveStateClass classification, int max = int.MaxValue)
        { Kind = kind; Classification = classification; MaximumInteger = max; }
    }

    public sealed class AlaalaDurableStateRegistry
    {
        private readonly SortedDictionary<string, AlaalaStateRule> rules = new SortedDictionary<string, AlaalaStateRule>(StringComparer.Ordinal);
        public IEnumerable<KeyValuePair<string, AlaalaStateRule>> Rules => rules;
        public bool TryGet(string key, out AlaalaStateRule rule)
        { rule = null; return key != null && rules.TryGetValue(key, out rule); }

        public AlaalaDurableStateRegistry(AlaalaSaveCatalog catalog)
        {
            if (catalog == null) throw new ArgumentException("Missing catalog.");
            if (!catalog.Validate(out var error)) throw new ArgumentException(error);
            // Reviewed gameplay readers/writers, not a name-pattern classification.
            AddWords(SaveStateClass.Story, @"
aling_ika_started aling_ika_payment_received aling_ika_completed randomhouse_maestro_lead_received maestro_ben_lead_received
maestro_ben_intro_seen maestro_ben_completed main_investigate_pili_started church_manuscript_investigated
makamisa_pili_intro_seen makamisa_church_intro_seen makamisa_anday_discovered makamisa_anday_first_conversation_complete
makamisa_anday_recollection_started makamisa_anday_recollection_complete makamisa_anday_post_recollection_payoff_applied
makamisa_part1_return_transport_started posada_first_sleep_completed aling_sela_intro_completed posada_morning_inventory_intro_completed
posada_part2_intro_cutscene_complete maestro_ben_part2_return_conversation_complete maestro_ben_part2_cecilia_ysagani_handoff_complete
alaala_part2_complete alaala_post_part2_draftworld_arrival_handled makamisa_kutsero_pasabi_complete makamisa_kalesa_travel_unlocked
makamisa_kutsero_pasabi_offer_available newmakamisa_miguel_find_kutsero_transition_complete
alaala_part3_sulatin_handoff_complete alaala_part3_light_revealed alaala_part3_light_reaction_complete
alaala_part3_light_transport_started alaala_part3_arrival_complete alaala_part3_ch8_dialogue_complete alaala_part3_ch9_dialogue_complete
alaala_part3_ch10_dialogue_complete alaala_part3_ch10_chase_started alaala_part3_ch11_first_dialogue_complete
alaala_part3_ch11_final_dialogue_complete alaala_part3_ch11_white_presented alaala_part3_ch11_complete
alaala_part3_ch12_dialogue_complete alaala_part3_ch12_complete alaala_part3_ch13_complete alaala_epilogue_dialogue_complete alaala_epilogue_complete
church_mang_lando_conversation_completed church_aling_marta_conversation_completed church_aling_pilar_conversation_completed
church_aling_elena_conversation_completed church_aling_rosa_conversation_completed church_mang_tomas_conversation_completed
church_mang_pedro_conversation_completed randomhouse_resident_information_completed randomhouse1_anday_information_completed
makamisa_church_overheard_1_complete makamisa_church_overheard_2_complete makamisa_church_overheard_3_complete
makamisa_fura_clodio_overheard_complete makamisa_lopez_paquito_overheard_complete makamisa_sebia_barang_overheard_complete
present_pili_archival_rizal_novel_pagsusuri_complete present_pili_antique_tagalog_espanol_manuscripts_pagsusuri_complete
present_pili_antique_letter_sealed_envelope_pagsusuri_complete present_pili_weathered_filipino_archive_note_pagsusuri_complete
church_lumang_sulatin_pagsusuri_complete journal_unlocked inventory_unlocked clarity_unlocked");
            AddWords(SaveStateClass.CompletedPresentation, @"
gather_information_reaction_seen posada_night_boundary_intro_seen present_church_boundary_intro_seen
draftworld_movement_tutorial_complete draftworld_run_tutorial_complete draftworld_jump_tutorial_complete
journal_tutorial_seen inventory_tutorial_seen inventory_tutorial_first_item_seen inventory_tutorial_intro_seen
clarity_tutorial_seen clarity_document_tutorial_seen inventory_tutorial_reveal_started inventory_tutorial_reveal_ready
maestro_ben_part2_cecilia_ysagani_presentation_complete alaala_part3_light_reaction_shown
alaala_part3_ch8_task_presented alaala_part3_ch9_task_presented alaala_part3_ch10_task_presented alaala_part3_ch12_ellipsis_complete");
            Add("maestro_ben_part2_cecilia_ysagani_presentation_phase", SaveValueKind.Integer, SaveStateClass.CompletedPresentation, 6);
            Add("draftworld_night_navigation_phase", SaveValueKind.Integer, SaveStateClass.CompletedPresentation, 4);
            Add("makamisa_kalesa_current_destination", SaveValueKind.String, SaveStateClass.Story);
            AddWords(SaveStateClass.Story, "makamisa_kalesa_at_bungad_ng_bayan makamisa_kalesa_at_plaza makamisa_kalesa_at_dulo_ng_bayan");
            AddWords(SaveStateClass.Temporary, @"alaala_epilogue_return_in_progress alaala_post_part2_draftworld_arrival_pending
alaala_part3_ch89_owner_active alaala_part3_ch10_owner_active alaala_part3_ch11_owner_active
journal.pending_entry_attention journal.has_unseen_entries");
            foreach (var key in new[] { "alaala_epilogue_return_mode", "alaala_part3_ch12_cinematic_step", "inventory_tutorial_intro_step", "inventory_tutorial_pending_phases", "inventory_tutorial_pending_step" })
                Add(key, SaveValueKind.Integer, SaveStateClass.Temporary);
            foreach (var key in new[] { "makamisa_kalesa_pending_destination", "makamisa_transport_destination" })
                Add(key, SaveValueKind.String, SaveStateClass.Temporary);
            Add("alaala_epilogue_development_test", SaveValueKind.Boolean, SaveStateClass.Development);

            foreach (var task in catalog.Tasks)
            {
                Add("task_active:" + task.TaskId, SaveValueKind.Boolean, SaveStateClass.Story);
                Add("task_completed:" + task.TaskId, SaveValueKind.Boolean, SaveStateClass.Story);
                Add("task_stage:" + task.TaskId, SaveValueKind.String, SaveStateClass.Story);
                foreach (var stage in task.Stages)
                    foreach (var progress in stage.AllowedProgressIds)
                    {
                        var key = "task_progress:" + task.TaskId + ":" + stage.StageId + ":" + progress;
                        // The task system intentionally writes the same key in two typed stores.
                        Add(key, SaveValueKind.Integer, SaveStateClass.Story, Math.Max(1, stage.RequiredCount));
                        progressKeys.Add(key);
                    }
            }
            foreach (var item in catalog.Items)
                foreach (var prefix in new[] { "inventory.notification.known:", "inventory.notification.read:" })
                    Add(prefix + item.ItemID, SaveValueKind.Boolean, SaveStateClass.CompletedPresentation);
            foreach (var entry in catalog.People) Journal("People", entry.personID, "person", entry.descriptionStages.Count);
            foreach (var entry in catalog.Observations) Journal("Observation", entry.observationID, "observation", 0);
            foreach (var entry in catalog.Fragments) Journal("Fragment", entry.fragmentID, "fragment", entry.interpretationStages.Count);
            foreach (var entry in catalog.Reflections)
            {
                Journal("Reflection", entry.reflectionID, "reflection", 0);
                Add("journal_reflection_response:" + entry.reflectionID, SaveValueKind.String, SaveStateClass.PlayerAuthored);
                Add("journal_reflection_follow_up:" + entry.reflectionID, SaveValueKind.String, SaveStateClass.CompletedPresentation);
            }
        }

        private readonly HashSet<string> progressKeys = new HashSet<string>(StringComparer.Ordinal);
        public bool Accepts(string key, SaveValueKind kind, bool durableOnly = true)
        {
            return TryGet(key, out var rule) && (!durableOnly || rule.Durable) &&
                (rule.Kind == kind || (kind == SaveValueKind.Boolean && progressKeys.Contains(key)));
        }
        private void Journal(string category, string id, string storage, int stages)
        {
            Add("journal_" + storage + "_unlocked:" + id, SaveValueKind.Boolean, SaveStateClass.Story);
            if (stages > 0) Add("journal_" + storage + "_stage:" + id, SaveValueKind.Integer, SaveStateClass.Story, stages - 1);
            Add("journal.e12.known:" + category + ":" + id, SaveValueKind.Boolean, SaveStateClass.CompletedPresentation);
            Add("journal.e12.read:" + category + ":" + id, SaveValueKind.Boolean, SaveStateClass.CompletedPresentation);
        }
        private void AddWords(SaveStateClass classification, string words)
        {
            foreach (var word in words.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)) Add(word, SaveValueKind.Boolean, classification);
        }
        private void Add(string key, SaveValueKind kind, SaveStateClass classification, int max = int.MaxValue)
        { rules.Add(key, new AlaalaStateRule(kind, classification, max)); }

        public bool Validate(AlaalaSessionSnapshot state, out string error)
        {
            error = null;
            if (state == null || state.booleans == null || state.integers == null || state.strings == null)
                return AlaalaSaveCatalog.Fail("Missing typed session lists.", out error);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in state.booleans)
                if (entry == null || !Accepts(entry.key, SaveValueKind.Boolean) || !seen.Add(entry.key))
                    return AlaalaSaveCatalog.Fail("Unknown, duplicate or non-durable Boolean key.", out error);
            seen.Clear();
            foreach (var entry in state.integers)
                if (entry == null || !Accepts(entry.key, SaveValueKind.Integer) || !seen.Add(entry.key) || entry.value < 0 || entry.value > rules[entry.key].MaximumInteger)
                    return AlaalaSaveCatalog.Fail("Unknown, duplicate or invalid integer key/value.", out error);
            seen.Clear();
            foreach (var entry in state.strings)
                if (entry == null || !Accepts(entry.key, SaveValueKind.String) || !seen.Add(entry.key) || entry.value == null || entry.value.Length > AlaalaSaveSchema.MaximumTextLength || entry.value.IndexOf('\0') >= 0)
                    return AlaalaSaveCatalog.Fail("Unknown, duplicate or malformed string key/value.", out error);
            return true;
        }
    }

    // Read-only projections make cross-system validation use the exact serialized values.
    internal sealed class AlaalaStateView
    {
        private readonly AlaalaSessionSnapshot state;
        internal AlaalaStateView(AlaalaSessionSnapshot state) { this.state = state; }
        internal bool Flag(string key) => state.booleans.Any(x => x.key == key && x.value);
        internal int Int(string key) => state.integers.FirstOrDefault(x => x.key == key)?.value ?? 0;
        internal string Text(string key) => state.strings.FirstOrDefault(x => x.key == key)?.value ?? "";
        internal int Progress(string key) => Int(key) > 0 ? Int(key) : Flag(key) ? 1 : 0;
    }
}
