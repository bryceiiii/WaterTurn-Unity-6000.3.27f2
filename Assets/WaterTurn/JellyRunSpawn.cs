using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunSpawn : MonoBehaviour
    {
        public Color gizmoColor = new Color(.45f, 1f, .9f, .9f);

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 14f);
        }
    }
}
