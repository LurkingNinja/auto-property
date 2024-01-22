# AutoProperty

Creates a property and change-events for fields decorated with the [AutoProperty] attribute.

## Installation

### Add package from git URL
Use the Package Manager's ```+/Add package from git URL``` function.
The URL you should use is this:
```
https://github.com/LurkingNinja/com.lurking-ninja.auto-property.git?path=Packages/com.lurking-ninja.auto-property
```

## Attributes
- AutoProperty
- PublicGet
- PublicSet
- PublicGetSet

## Basic usage
Consider this test class:
```csharp
using AutoPropertyAttribute;
using UnityEngine;

namespace Scenes
{
    public partial class TestAutoProperty : MonoBehaviour
    {
        [SerializeField][AutoProperty]
        private int strength;

        private void Awake() => OnStrengthChange += OnStrChange;

        private void Start() => Strength++;

        private static void OnStrChange(int newValue) => Debug.Log(newValue);
    }
}
```
This will provide this generated code in the ```TestAutoProperty_codegen.cs```:
```csharp
namespace Scenes{
    public partial class TestAutoProperty {
        public event System.Action OnAnyChanged;
        public event System.Action<int> OnStrengthChange;
        public int Strength { 
            get => strength;
            set { strength = value; 
                OnStrengthChange?.Invoke(value);
                OnAnyChanged?.Invoke();
            }
        }
    }
}
```