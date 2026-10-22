using System.Linq;
using Alaala.Saving;

public partial class ReconstructionPeople
{
    internal bool InstallDiscoveredSaveAssets(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state)
    {
        var saved = catalog.People.Where(p => state.booleans.Any(b => b.value && b.key == "journal_person_unlocked:" + p.personID)).ToArray();
        if (saved.Any(p => allPeople.Any(x => x != null && x.personID == p.personID && x != p))) return false;
        foreach (var entry in saved) if (!allPeople.Contains(entry)) allPeople.Add(entry);
        return true;
    }
}
public partial class ReconstructionObservations
{
    internal bool InstallDiscoveredSaveAssets(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state)
    {
        var saved = catalog.Observations.Where(p => state.booleans.Any(b => b.value && b.key == "journal_observation_unlocked:" + p.observationID)).ToArray();
        if (saved.Any(p => allObservations.Any(x => x != null && x.observationID == p.observationID && x != p))) return false;
        foreach (var entry in saved) if (!allObservations.Contains(entry)) allObservations.Add(entry);
        return true;
    }
}
public partial class ReconstructionFragments
{
    internal bool InstallDiscoveredSaveAssets(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state)
    {
        var saved = catalog.Fragments.Where(p => state.booleans.Any(b => b.value && b.key == "journal_fragment_unlocked:" + p.fragmentID)).ToArray();
        if (saved.Any(p => allFragments.Any(x => x != null && x.fragmentID == p.fragmentID && x != p))) return false;
        foreach (var entry in saved) if (!allFragments.Contains(entry)) allFragments.Add(entry);
        return true;
    }
}
public partial class ReconstructionReflections
{
    internal bool InstallDiscoveredSaveAssets(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state)
    {
        var saved = catalog.Reflections.Where(p => state.booleans.Any(b => b.value && b.key == "journal_reflection_unlocked:" + p.reflectionID)).ToArray();
        if (saved.Any(p => allReflections.Any(x => x != null && x.reflectionID == p.reflectionID && x != p))) return false;
        foreach (var entry in saved) if (!allReflections.Contains(entry)) allReflections.Add(entry);
        return true;
    }
}
public partial class ReconstructionJournalManager
{
    internal bool PrepareSaveAssets(AlaalaSaveCatalog catalog, AlaalaSessionSnapshot state, out string error)
    {
        error = null;
        if (!AlaalaSaveFoundation.RestorationInProgress || people == null || observations == null || fragments == null || reflections == null)
        { error = "Journal restoration context/components are missing."; return false; }
        if (!new AlaalaDurableStateRegistry(catalog).Validate(state, out error)) return false;
        if (!people.InstallDiscoveredSaveAssets(catalog, state) || !observations.InstallDiscoveredSaveAssets(catalog, state) ||
            !fragments.InstallDiscoveredSaveAssets(catalog, state) || !reflections.InstallDiscoveredSaveAssets(catalog, state))
        { error = "Scene journal conflicts with approved saved content."; return false; }
        return CanHydrateSaveJournal(catalog, state, out error);
    }

    internal void RefreshRestoredViews()
    {
        people.RefreshList(); observations.RefreshList(); fragments.RefreshList(); reflections.RefreshList();
        RefreshCategoryUnreadBadges();
    }
}
