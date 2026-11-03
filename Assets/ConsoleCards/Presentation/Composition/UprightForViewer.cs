using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// QA Q12 (2026-11-03): a game's pile labels (ABILITIES, AVATARS…) lay facing one seat, so the players opposite
    /// read them upside down. A label with this turns, flat on the table, to read upright from this computer's
    /// camera. Only its own rotation changes, and only when the camera has turned.
    /// </summary>
    internal sealed class UprightForViewer : MonoBehaviour
    {
        private UnityEngine.Camera viewer;
        private Vector3 lastUp;

        private void LateUpdate()
        {
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = UnityEngine.Camera.main;
            if (viewer == null) return;
            Vector3 up = Vector3.ProjectOnPlane(viewer.transform.up, Vector3.up);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up);
            if (up.sqrMagnitude < 0.0001f) return;
            up.Normalize();
            if ((up - lastUp).sqrMagnitude < 0.0004f) return;
            lastUp = up;
            transform.rotation = Quaternion.LookRotation(Vector3.down, up);
        }
    }
}
