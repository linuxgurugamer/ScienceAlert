using ReeperCommon;

namespace ScienceAlert.ProfileData
{
    public class ExperimentSettings
    {
        public enum FilterMethod
        {
            Unresearched,
            NotMaxed,
            LessThanFiftyPercent,
            LessThanNinetyPercent
        }

        private bool _enabled = true;
        private bool _soundOnDiscovery = true;
        private bool _animationOnDiscovery = true;
        private bool _stopWarpOnDiscovery;
        private FilterMethod _filter;
        public bool IsDefault;
        public event Callback OnChanged = delegate{};

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            set
            {
                if (value == _enabled) return;
                _enabled = value;
                OnChanged();
            }
        }

        public bool SoundOnDiscovery
        {
            get
            {
                return _soundOnDiscovery;
            }
            set
            {
                if (_soundOnDiscovery != value)
                {
                    _soundOnDiscovery = value;
                    OnChanged();
                }
            }
        }

        public bool AnimationOnDiscovery
        {
            get
            {
                return _animationOnDiscovery;
            }
            set
            {
                if (value != _animationOnDiscovery)
                {
                    _animationOnDiscovery = value;
                    OnChanged();
                }
            }
        }

        public bool StopWarpOnDiscovery
        {
            get
            {
                return _stopWarpOnDiscovery;
            }
            set
            {
                if (value != _stopWarpOnDiscovery)
                {
                    _stopWarpOnDiscovery = value;
                    OnChanged();
                }
            }
        }

        public FilterMethod Filter
        {
            get
            {
                return _filter;
            }
            set
            {
                if (value != _filter)
                {
                    _filter = value;
                    OnChanged();
                }
            }
        }

        public ExperimentSettings()
        {
        }

        public ExperimentSettings(ExperimentSettings other)
        {
            Enabled = other.Enabled;
            SoundOnDiscovery = other.SoundOnDiscovery;
            AnimationOnDiscovery = other.AnimationOnDiscovery;
            StopWarpOnDiscovery = other.StopWarpOnDiscovery;
            Filter = other.Filter;
            IsDefault = other.IsDefault;
        }

        public void OnLoad(ConfigNode node)
        {
            Enabled = node.Parse("Enabled", true);
            SoundOnDiscovery = node.Parse("SoundOnDiscovery", true);
            AnimationOnDiscovery = node.Parse("AnimationOnDiscovery", true);
            StopWarpOnDiscovery = node.Parse("StopWarpOnDiscovery", false);
            string value = node.GetValue("Filter");
            if (string.IsNullOrEmpty(value))
            {
                Log.Debug("[ScienceAlert]:Settings: invalid experiment filter");
                value = System.Enum.GetValues(typeof(FilterMethod)).GetValue(0).ToString();
            }
            Filter = (FilterMethod)System.Enum.Parse(typeof(FilterMethod), value);
            IsDefault = node.Parse("IsDefault", false);
        }

        public void OnSave(ConfigNode node)
        {
            node.AddValue("Enabled", Enabled);
            node.AddValue("SoundOnDiscovery", SoundOnDiscovery);
            node.AddValue("AnimationOnDiscovery", AnimationOnDiscovery);
            node.AddValue("StopWarpOnDiscovery", StopWarpOnDiscovery);
            node.AddValue("Filter", Filter);
            node.AddValue("IsDefault", IsDefault);
        }

        public override bool Equals(object obj)
        {
            return obj is ExperimentSettings es && Enabled == es.Enabled && SoundOnDiscovery ==
                es.SoundOnDiscovery && AnimationOnDiscovery == es.AnimationOnDiscovery &&
                StopWarpOnDiscovery == es.StopWarpOnDiscovery && Filter == es.Filter &&
                IsDefault == es.IsDefault;
        }

        public override int GetHashCode()
        {
            return (Enabled ? 0x1 : 0x0) | (SoundOnDiscovery ? 0x2 : 0x0) |
                (AnimationOnDiscovery ? 0x4 : 0x0) | (StopWarpOnDiscovery ? 0x8 : 0x0) |
                (IsDefault ? 0x10 : 0x0) | ((int)Filter << 8);
        }

        public override string ToString()
        {
            ConfigNode configNode = new ConfigNode();
            OnSave(configNode);
            return configNode.ToString();
        }
    }
}
