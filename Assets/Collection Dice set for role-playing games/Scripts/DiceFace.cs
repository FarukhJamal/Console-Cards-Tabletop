using UnityEngine;

public class DiceFace : MonoBehaviour
{
    [Header("Face Value")]
    public int value;

    [Header("Face Direction")]
    [Tooltip("Transform whose FORWARD direction points outward from this face.")]
    public Transform faceDirection;
}