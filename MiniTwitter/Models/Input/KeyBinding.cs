using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Xml.Serialization;

namespace MiniTwitter.Models.Input
{
    [Serializable]
    public class KeyBinding : PropertyChangedBase, IEquatable<KeyBinding>, IEditableObject
    {
        public KeyBinding()
        {
        }

        private KeyAction _action;

        [XmlAttribute]
        public KeyAction Action
        {
            get { return _action; }
            set
            {
                if (_action != value)
                {
                    _action = value;
                    OnPropertyChanged("Action");
                }
            }
        }

        private KeyActionSpot _actionSpot;

        [XmlAttribute]
        public KeyActionSpot ActionSpot
        {
            get { return _actionSpot; }
            set
            {
                if (_actionSpot != value)
                {
                    _actionSpot = value;
                    OnPropertyChanged("ActionSpot");
                }
            }
        }

        private Key key;

        [XmlAttribute]
        public Key Key
        {
            get { return key; }
            set
            {
                if (key != value)
                {
                    key = value;
                    OnPropertyChanged("Key");
                }
            }
        }

        private ModifierKeys _modifierKeys;

        [XmlAttribute]
        public ModifierKeys ModifierKeys
        {
            get { return _modifierKeys; }
            set
            {
                if (_modifierKeys != value)
                {
                    _modifierKeys = value;
                    OnPropertyChanged("ModifierKeys");
                }
            }
        }

        public override int GetHashCode()
        {
            return Action.GetHashCode();
        }

        public bool Equals(KeyBinding other)
        {
            return Action == other.Action;
        }

        private KeyBinding _copy;

        public void BeginEdit()
        {
            if (_copy == null)
            {
                _copy = new KeyBinding();
            }
            _copy.Action = this.Action;
            _copy.ActionSpot = this.ActionSpot;
            _copy.Key = this.Key;
            _copy.ModifierKeys = this.ModifierKeys;
        }

        public void CancelEdit()
        {
            this.Action = _copy.Action;
            this.ActionSpot = _copy.ActionSpot;
            this.Key = _copy.Key;
            this.ModifierKeys = _copy.ModifierKeys;
        }

        public void EndEdit()
        {
            _copy.ActionSpot = KeyActionSpot.All;
            _copy.Key = Key.None;
            _copy.ModifierKeys = ModifierKeys.None;
        }
    }
}
