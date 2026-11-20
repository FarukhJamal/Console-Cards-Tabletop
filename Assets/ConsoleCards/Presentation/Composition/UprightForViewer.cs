using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// QA Q12 (2026-11-03): a game's pile labels (ABILITIES, AVATARS…) lay facing one seat, so the players opposite
    /// read them upside down. A label with this turns, flat on the table, to read upright from this computer's
    /// camera. Only its own rotation changes.
    /// Designer note 02 (2026-11-17): the table's and the card zones' labels keep turning with the camera, so it is
    /// checked every frame (its parent may turn too, or set the label's own rotation again); it is set only when it is
    /// more than half a degree off. Does not allocate.
    /// </summary>
    internal sealed class UprightForViewer : MonoBehaviour
    {
        private UnityEngine.Camera viewer;

        private void LateUpdate()
        {
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = UnityEngine.Camera.main;
            if (viewer == null) return;
            Vector3 up = Vector3.ProjectOnPlane(viewer.transform.up, Vector3.up);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up);
            if (up.sqrMagnitude < 0.0001f) return;
            up.Normalize();
            Quaternion wanted = Quaternion.LookRotation(Vector3.down, up);
            if (Quaternion.Angle(transform.rotation, wanted) > 0.5f) transform.rotation = wanted;
        }

        /// <summary>Designer note 02: makes the label turn to read upright from this computer's camera.</summary>
        internal static void Keep(Component label)
        {
            if (label != null && label.GetComponent<UprightForViewer>() == null) label.gameObject.AddComponent<UprightForViewer>();
        }
    }
}
