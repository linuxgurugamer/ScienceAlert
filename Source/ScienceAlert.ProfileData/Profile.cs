using System.Collections.Generic;
using ReeperCommon;
using UnityEngine;

namespace ScienceAlert.ProfileData
{
    internal class Profile
    {
        [Persistent(isPersistant = true)]
        public string name = string.Empty;

        [Persistent]
        public bool modified;

        [Persistent]
        public float scienceThreshold;

        [System.NonSerialized]
        public Dictionary<string, ExperimentSettings> settings;

        public ExperimentSettings this[string expid]
        {
            get
            {
                if (settings.ContainsKey(expid))
                {
                    return settings[expid];
                }
                settings[expid] = new ExperimentSettings();
                return settings[expid];
            }
            private set
            {
                settings.Add(expid.ToLower(), value);
            }
        }

        public string DisplayName
        {
            get
            {
                if (modified)
                {
                    return "*" + name + "*";
                }
                return name;
            }
        }

        public float ScienceThreshold
        {
            get
            {
                return scienceThreshold;
            }
            set
            {
                if (value != scienceThreshold)
                {
                    modified = true;
                }
                scienceThreshold = value;
            }
        }

        public Profile(ConfigNode node)
        {
            Setup();
            OnLoad(node);
            RegisterEvents();
        }

        public Profile(string name)
        {
            Log.Debug("VERB ALERT:Creating profile '{0}' with default values", name);
            this.name = name;
            Setup();
            RegisterEvents();
        }

        public Profile(Profile other)
        {
            Dictionary<string, ExperimentSettings>.KeyCollection keys = other.settings.Keys;
            settings = new Dictionary<string, ExperimentSettings>();

            foreach (string current in keys)
            {
                settings.Add(current, new ExperimentSettings(other.settings[current]));
            }
            name = string.Copy(other.name);
            modified = other.modified;
            scienceThreshold = other.scienceThreshold;
            RegisterEvents();
        }

        private void Setup()
        {
            settings = new Dictionary<string, ExperimentSettings>();
            try
            {
                List<string> experimentIDs = ResearchAndDevelopment.GetExperimentIDs();
                for (int i = experimentIDs.Count - 1; i >= 0; i--)
                {
                    string current = experimentIDs[i];

                    settings.Add(current, new ExperimentSettings());
                }
            }
            catch (System.Exception ex)
            {
                Log.Debug("[ScienceAlert]:Profile '{1}' constructor exception: {0}", ex, string.IsNullOrEmpty(name) ? "(unnamed)" : name);
            }
        }

        public void OnSave(ConfigNode node, bool writeContents)
        {
            ConfigNode.CreateConfigFromObject(this, 0, node);
            if (writeContents)
            {
                foreach (KeyValuePair<string, ExperimentSettings> current in settings)
                {
                    ConfigNode newNode = new ConfigNode(current.Key);
                    node.AddNode(newNode);
                    current.Value.OnSave(newNode);
                }
            }
           //Log.Debug("ALERT:Profile: OnSave config: {0}", node.ToString());
        }

        public void OnLoad(ConfigNode node)
        {
            Log.Debug("ALERT:Loading profile...");
            ConfigNode.LoadObjectFromConfig(this, node);
            if (string.IsNullOrEmpty(name))
            {
                name = "nameless." + System.Guid.NewGuid();
            }
            else
            {
                Log.Debug("ALERT:Profile name is '{0}'", name);
            }
            string[] array = node.nodes.DistinctNames();
            for (int i = 0; i < array.Length; i++)
            {
                string text = array[i];
                ConfigNode node2 = node.GetNode(text);
                if (!settings.ContainsKey(text))
                {
                    settings.Add(text, new ExperimentSettings());
                }
                settings[text].OnLoad(node2);
            }
        }

        public override bool Equals(object obj)
        {
            bool eql = false;
            IDictionary<string, ExperimentSettings> otherSettings;
            if (obj is Profile other && name == other.name && Mathf.Approximately(
                scienceThreshold, other.scienceThreshold) && (otherSettings = other.settings).
                Count == settings.Count)
            {
                eql = true;
                foreach (var pair in settings)
                {
                    if (!otherSettings.TryGetValue(pair.Key, out ExperimentSettings os) ||
                        !pair.Value.Equals(os))
                    {
                        eql = false;
                        break;
                    }
                    // If all of ours exist in theirs, and same size, then they can have no
                    // imposters
                }
            }
            return eql;
        }

        public override int GetHashCode()
        {
            return name.GetHashCode();
        }

        public Profile Clone()
        {
            return new Profile(this);
        }

        public static Profile MakeDefault()
        {
            return new Profile("default");
        }

        private void SettingChanged()
        {
            Log.Debug("ALERT:Profile '{0}' was modified!", name);
            modified = true;
        }

        private void RegisterEvents()
        {
            foreach (KeyValuePair<string, ExperimentSettings> current in settings)
            {
                current.Value.OnChanged += SettingChanged;
            }
        }
    }
}
