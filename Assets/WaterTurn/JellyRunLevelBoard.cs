using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunLevelBoard : MonoBehaviour
    {
        public RectTransform worldRect;

        void Awake()
        {
            if (worldRect == null) worldRect = (RectTransform)transform;
        }

        void OnValidate()
        {
            if (worldRect == null) worldRect = (RectTransform)transform;
        }
    }
}
