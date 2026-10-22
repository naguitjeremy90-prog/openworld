using System.Linq;
using Alaala.Saving;

public partial class ReconstructionPeople
{
    internal bool SupportsSaveCatalog(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state) => catalog.People.All(x =>
        !state.booleans.Any(b => b.key == "journal_person_unlocked:" + x.personID && b.value) || allPeople.Contains(x));
    internal void HydrateSaveCacheSilently() { selectedPerson = null; RestoreSessionState(); }
}
public partial class ReconstructionObservations
{
    internal bool SupportsSaveCatalog(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state) => catalog.Observations.All(x =>
        !state.booleans.Any(b => b.key == "journal_observation_unlocked:" + x.observationID && b.value) || allObservations.Contains(x));
    internal void HydrateSaveCacheSilently() { selectedObservation = null; RestoreSessionState(); }
}
public partial class ReconstructionFragments
{
    internal bool SupportsSaveCatalog(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state) => catalog.Fragments.All(x =>
        !state.booleans.Any(b => b.key == "journal_fragment_unlocked:" + x.fragmentID && b.value) || allFragments.Contains(x));
    internal void HydrateSaveCacheSilently() { selectedFragment = null; RestoreSessionState(); }
}
public partial class ReconstructionReflections
{
    internal bool SupportsSaveCatalog(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state) => catalog.Reflections.All(x =>
        !state.booleans.Any(b => b.key == "journal_reflection_unlocked:" + x.reflectionID && b.value) || allReflections.Contains(x));
    internal void HydrateSaveCacheSilently() { selectedReflection = null; RestoreSessionState(); }
}
public partial class ReconstructionJournalManager
{
    public bool CanHydrateSaveJournal(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state, out string error)
    {
        error = null;
        if (catalog == null || !catalog.Validate(out error)) return false;
        if (!new AlaalaDurableStateRegistry(catalog).Validate(state, out error)) return false;
        if (IsOpen || reflections == null || reflections.IsFinalSubmissionPresentationActive || observations == null || people == null || fragments == null)
        { error = "Journal must be complete, closed and idle for hydration."; return false; }
        if (!people.SupportsSaveCatalog(catalog, state) || !observations.SupportsSaveCatalog(catalog, state) ||
            !fragments.SupportsSaveCatalog(catalog, state) || !reflections.SupportsSaveCatalog(catalog, state))
        { error = "Scene journal catalog is incomplete; restoration is unsupported."; return false; }
        return true;
    }

    public bool TryHydrateSaveJournalSilently(AlaalaSessionSnapshot state, AlaalaSaveCatalog catalog, out string error)
    {
        if (!CanHydrateSaveJournal(catalog, state, out error)) return false;
        if (!SessionStoryState.MatchesDurableSnapshot(state, catalog, out error)) return false;
        people.HydrateSaveCacheSilently(); observations.HydrateSaveCacheSilently();
        fragments.HydrateSaveCacheSilently(); reflections.HydrateSaveCacheSilently();
        // No unlock methods, entry events, animations or list-button recreation.
        return true;
    }
}
