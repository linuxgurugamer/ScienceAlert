using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ReeperCommon;
using UnityEngine;

namespace ScienceAlert.ProfileData
{
    using ProfileTable = Dictionary<string, Profile>;
    using VesselTable = Dictionary<Guid, Profile>;

    [KSPScenario(ScenarioCreationOptions.AddToExistingCareerGames |
                 ScenarioCreationOptions.AddToExistingScienceSandboxGames |
                 ScenarioCreationOptions.AddToNewCareerGames |
                 ScenarioCreationOptions.AddToNewScienceSandboxGames,
        GameScenes.FLIGHT)]
    class ScienceAlertProfileManager : ScenarioModule
    {
        private readonly string ProfileStoragePath = ConfigUtil.GetDllDirectoryPath() + "/../PluginData/profiles.cfg";
        ProfileTable storedProfiles;
        VesselTable vesselProfiles;

        private const string PERSISTENT_NODE_NAME = "ScienceAlert_Profiles";
        private const string STORED_NODE_NAME = "Stored_Profiles";

        #region intialization/deinitialization

        public override void OnAwake()
        {
            base.OnAwake();
            if (HighLogic.CurrentGame.config == null)
                HighLogic.CurrentGame.config = new ConfigNode();

            Settings.Instance.OnSave += OnSettingsSave; // this triggers saving of stored profiles

            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            GameEvents.onVesselCreate.Add(OnVesselCreate);
#if false
            GameEvents.onVesselWasModified.Add(OnVesselModified);
            GameEvents.onFlightReady.Add(OnFlightReady);
            GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroy);
            GameEvents.onSameVesselUndock.Add(OnSameVesselUndock);
            GameEvents.onUndock.Add(OnUndock);
#endif

            Ready = false; // won't be ready until OnLoad
            Instance = this;
            LoadStoredProfiles();
        }

        private void OnDestroy()
        {
            Instance = null;
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
#if false
            GameEvents.onVesselWasModified.Remove(OnVesselModified);
            GameEvents.onFlightReady.Remove(OnFlightReady);
            GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroy);
            GameEvents.onSameVesselUndock.Remove(OnSameVesselUndock);
            GameEvents.onUndock.Remove(OnUndock);
#endif
            SaveStoredProfiles();
        }

        private void LoadStoredProfiles()
        {
            try
            {
                storedProfiles = new ProfileTable();
                if (System.IO.File.Exists(ProfileStoragePath))
                {
                    ConfigNode stored = ConfigNode.Load(ProfileStoragePath);
                    if (stored != null && stored.HasNode(STORED_NODE_NAME))
                    {
                        stored = stored.GetNode(STORED_NODE_NAME); // to avoid having an empty cfg, which will cause KSP to hang at load
                        var profiles = stored.GetNodes("PROFILE");

                        foreach (var profileNode in profiles)
                        {
                            try
                            {
                                Profile p = new Profile(profileNode);
                                p.modified = false; // by definition, stored profiles haven't been modified
                                storedProfiles.Add(p.name, p);
                                Log.Normal("[ScienceAlert]Loaded profile '{0}' successfully!", p.name);
                            }
                            catch (Exception e)
                            {
                                Log.Error("ProfileManager: profile '{0}' failed to parse; {1}", name, e);
                            }
                        }
                    }
                }
                if (DefaultProfile == null)
                    storedProfiles.Add("default", Profile.MakeDefault());

            }
            catch (Exception e)
            {
                Log.Error("ProfileManager: Exception loading stored profiles: {0}", e);
                storedProfiles = new ProfileTable();
            }
        }

        private void SaveStoredProfiles()
        {
            ConfigNode profiles = new ConfigNode(STORED_NODE_NAME); // note: gave it a name because an empty ConfigNode will cause KSP to choke on load
            foreach (var kvp in storedProfiles)
            {
                try
                {
                    kvp.Value.OnSave(profiles.AddNode(new ConfigNode("PROFILE")), true);
                }
                catch (Exception e)
                {
                    Log.Error("ProfileManager: Exception while saving '{0}': {1}", kvp.Key, e);
                }
            }
            System.IO.File.WriteAllText(ProfileStoragePath, profiles.ToString());
        }

#endregion

#region GameEvents

        private void OnVesselChange(Vessel vessel)
        {
            if (vessel == null) return;
            StartCoroutine(WaitForLoad(vessel));
        }
        IEnumerator WaitForLoad(Vessel vessel)
        {
            while (!Ready)
                yield return new WaitForSeconds(0.1f);

            if (!vesselProfiles.ContainsKey(vessel.id)) yield break;
            if (vesselProfiles[vessel.id].modified) yield break;

            var stored = FindStoredProfile(vesselProfiles[vessel.id].name);
            if (stored == null)
            {
                vesselProfiles[vessel.id].modified = true;
            }
            else
            {
                Log.Normal("ProfileManager.OnVesselChange: Bringing vessel {0} up to date on stored profile {1}", vessel.id, stored.name);
                vesselProfiles[vessel.id] = stored.Clone();
            }
        }

        private void OnVesselDestroy(Vessel vessel)
        {
            if (!vesselProfiles.ContainsKey(vessel.id)) return;
            vesselProfiles.Remove(vessel.id);
        }

        private void OnVesselCreate(Vessel newVessel)
        {
            if (newVessel == null) return;

            try
            {
                if (vesselProfiles == null) return; // we haven't even init yet
                if (!newVessel.loaded) return;
                if (newVessel.protoVessel == null) return;
                if (newVessel.protoVessel.protoPartSnapshots.Count == 0) return;
                if (FlightGlobals.ActiveVessel == newVessel || newVessel.vesselType == VesselType.Debris) return;

                Profile parentProfile = null;
                uint mid = newVessel.packed ? newVessel.protoVessel.protoPartSnapshots[newVessel.protoVessel.rootIndex].missionID : newVessel.rootPart.missionID;

                if (mid == FlightGlobals.ActiveVessel.rootPart.missionID)
                    if (vesselProfiles.ContainsKey(FlightGlobals.ActiveVessel.id))
                        if (vesselProfiles[FlightGlobals.ActiveVessel.id] == ActiveProfile)
                            parentProfile = ActiveProfile;

                if (parentProfile == null)
                {
                    var parentVessel = FlightGlobals.Vessels.SingleOrDefault(v =>
                    {
                        if (v.rootPart == null) return false;
                        if (mid != v.rootPart.missionID) return false;
                        return vesselProfiles.ContainsKey(v.id);
                    });

                    if (parentVessel != null) parentProfile = vesselProfiles[parentVessel.id];
                }

                if (parentProfile == null) return;
                if (vesselProfiles.ContainsKey(newVessel.id)) return;
                vesselProfiles.Add(newVessel.id, parentProfile.Clone());
            }
            catch (Exception e)
            {
                Log.Error("ProfileManager.OnVesselCreate: Something went wrong while handling this event; {0}", e);
            }
        }

#if false
        private void OnVesselModified(Vessel vessel)
        {
            Log.Debug("ProfileManager.OnVesselModified: {0}", vessel.vesselName);
        }

        private void OnFlightReady()
        {
            Log.Debug("ProfileManager.OnFlightReady");
        }

        private void OnVesselWillDestroy(Vessel vessel)
        {
            Log.Debug("ProfileManager.OnVesselWillDestroy: {0}", vessel.vesselName);
        }

        private void OnSameVesselUndock(GameEvents.FromToAction<ModuleDockingNode, ModuleDockingNode> nodes)
        {
            Log.Debug("ProfileManager.OnSameVesselUndock: from {0}, to {1}", nodes.from.vessel.vesselName, nodes.to.vessel.vesselName);
        }

        private void OnUndock(EventReport report)
        {
            Log.Debug("ProfileManager.OnUndock: origin {0}, sender {1}", report.origin.name, report.sender);
        }
#endif

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!node.HasNode(PERSISTENT_NODE_NAME))
            {
                Log.Warning("Persistent save has no saved profiles");
                vesselProfiles = new VesselTable();
                Ready = true;
                return;
            }
            savedNode = node.CreateCopy();
            StartCoroutine(WaitForFlightGlobals());
        }
        ConfigNode savedNode;

        IEnumerator WaitForFlightGlobals()
        {
            while (!FlightGlobals.ready || (FlightGlobals.ActiveVessel == null))
                yield return new WaitForSeconds(0.1f); 
            ConfigNode node = savedNode;

            node = node.GetNode(PERSISTENT_NODE_NAME);
            vesselProfiles = new VesselTable();
            var guidStrings = node.nodes.DistinctNames();

            foreach (var strGuid in guidStrings)
            {
                try
                {
                    Guid guid = new Guid(strGuid);  // could throw an exception if string is malformed


                    if (!FlightGlobals.fetch.vessels.Any(v => v.id == guid)) continue;

                    if (vesselProfiles.ContainsKey(guid)) continue;

                    ConfigNode profileNode = node.GetNode(strGuid);
                    Profile p = new Profile(profileNode);

                    if (p.modified)
                        vesselProfiles.Add(guid, p);
                    else
                    {
                        var storedProfile = FindStoredProfile(p.name);
                        if (storedProfile != null)
                        {
                            vesselProfiles.Add(guid, storedProfile.Clone());
                        }
                        else
                        {
                            p.modified = true;
                            vesselProfiles.Add(guid, p);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("ProfileManager: Exception while loading '{0}': {1}", strGuid, e);
                }
            }
            Ready = true;
        }

        public override void OnSave(ConfigNode node)
        {
            if (!HighLogic.LoadedSceneIsGame || HighLogic.CurrentGame == null)
                return;
            base.OnSave(node);
            var seen = new HashSet<string>(Instance.storedProfiles.Count * 2);
            if (!node.HasNode(PERSISTENT_NODE_NAME)) node.AddNode(PERSISTENT_NODE_NAME);
            node = node.GetNode(PERSISTENT_NODE_NAME);

            foreach (var kvp in vesselProfiles)
            {
                Guid uid = kvp.Key;
                var profile = kvp.Value;
                try
                {
                    if (FlightGlobals.Vessels.Any(v => v.id == uid))
                    {
                        var newNode = new ConfigNode(uid.ToString());
                        node.AddNode(newNode);
                        profile.OnSave(newNode, profile.modified || !seen.Contains(profile.
                            name));
                        seen.Add(profile.name);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("ProfileManager.OnSave: Exception while saving profile '{0}': {1}",
                        $"{uid}:{profile.name}", e);
                }
            }
            seen.Clear();
        }

#endregion

#region other events

        public void OnSettingsSave()
        {
            SaveStoredProfiles();
        }

#endregion

#region Interaction methods
        public static ScienceAlertProfileManager Instance { private set; get; }
        public bool Ready { private set; get; }

        public static Profile DefaultProfile
        {
            get
            {
                var key = Instance.storedProfiles.Keys.SingleOrDefault(k => k.ToLower().Equals("default"));
                if (string.IsNullOrEmpty(key))
                {
                    key = "default";
                    Instance.storedProfiles.Add(key, Profile.MakeDefault());
                }
                return Instance.storedProfiles[key];
            }
        }

        public static Profile ActiveProfile
        {
            get
            {
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER || FlightGlobals.ActiveVessel == null || Instance.vesselProfiles ==null)
                    return null;
                var vessel = FlightGlobals.ActiveVessel;
                
                if (!Instance.vesselProfiles.ContainsKey(vessel.id))
                {
                    Instance.vesselProfiles.Add(vessel.id, DefaultProfile.Clone());
                }
                return Instance.vesselProfiles[vessel.id];
            }
        }

        public static bool HasActiveProfile => FlightGlobals.ActiveVessel != null;

        public static int Count => Instance.storedProfiles != null ? Instance.storedProfiles.Count : 0;

        public static ProfileTable.KeyCollection Names => Instance.storedProfiles.Keys;

        public static Profile GetProfileByName(string name)
        {
            return FindStoredProfile(name);
        }

        public static ProfileTable Profiles => Instance.storedProfiles;

        public static void StoreActiveProfile(string name)
        {
            Profile p = ActiveProfile;
            p.name = name;
            p.modified = false;
            Profile newProfile = p.Clone();

            // If a profile already exists with this name (e.g. if saving the active profile and the name is unchanged) then remove it first.
            DeleteProfile(name);

            Instance.storedProfiles.Add(name, newProfile);
        }

        public static void DeleteProfile(string name)
        {
            var p = FindStoredProfile(name);
            if (p == null) return;
            Instance.storedProfiles.Remove(name);
        }

        public static void RenameProfile(string oldName, string newName)
        {
            var p = FindStoredProfile(oldName);

            if (p == null) return;
            if (DefaultProfile.Equals(p))
            {
                var cloned = p.Clone();
                cloned.name = newName;
                AssignAsActiveProfile(cloned);
                cloned.modified = p.modified;
                if (!cloned.modified)
                    StoreActiveProfile(newName);
            }
            else
            {
                p.name = newName;
            }
        }

        public static bool LoadStoredAsActiveProfile(string name)
        {
            var p = FindStoredProfile(name);
            if (p == null) return false;
            if (FlightGlobals.ActiveVessel == null) return false;
            Profile newProfile = p.Clone();
            newProfile.modified = false; // should already be false, just making sure
            Instance.vesselProfiles[FlightGlobals.ActiveVessel.id] = newProfile;
            return true;
        }

        public static bool AssignAsActiveProfile(Profile p)
        {
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return false;
            if (p == null) return false;
            Instance.vesselProfiles[vessel.id] = p;
            return true;
        }

#endregion

#region internal methods

        private static Profile FindStoredProfile(string name)
        {
            var key = Instance.storedProfiles.Keys.SingleOrDefault(k => k.ToLower().Equals(name.ToLower()));
            return string.IsNullOrEmpty(key) ? null : Instance.storedProfiles[key];
        }

        public static bool HaveStoredProfile(string name)
        {
            return FindStoredProfile(name) != null;
        }

        private string FindVesselName(Guid guid)
        {
            Vessel vessel = FlightGlobals.Vessels.SingleOrDefault(v => v.id == guid);
            if (vessel == null) return $"<vessel {guid} not found>";
            return vessel.vesselName;
        }

        private string VesselIdentifier(Guid guid)
        {
            return $"{guid}:{FindVesselName(guid)}";
        }
#endregion
    }
}
