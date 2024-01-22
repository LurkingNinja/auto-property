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
