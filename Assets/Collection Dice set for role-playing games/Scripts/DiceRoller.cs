using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class DiceRoller : MonoBehaviour
{
    public enum DiceType
    {
        D4,
        D6,
        D8,
        D10,
        D12,
        D20
    }

    [Header("Dice")]
    public DiceType diceType;

    [Header("References")]
    public Rigidbody rb;

    [Tooltip("All numbered faces of this dice.")]
    public DiceFace[] faces;

    [Header("Dice Box")]
    [Tooltip("Center point of the dice box.")]
    public Transform diceCenter;

    [Header("Roll Settings")]
    [SerializeField]
    private float rollForce = 5f;

    [SerializeField]
    private float torqueForce = 10f;

    [Header("Stop Detection")]
    [SerializeField]
    private float slowVelocity = 0.4f;

    [SerializeField]
    private float slowAngularVelocity = 0.4f;

    [SerializeField]
    private float stopVelocity = 0.05f;

    [SerializeField]
    private float stopAngularVelocity = 0.05f;

    [SerializeField]
    private float requiredStillTime = 0.25f;

    [Header("Centering")]
    [SerializeField]
    private float centerDistance = 0.4f;

    [SerializeField]
    private float centerDuration = 0.5f;

    [SerializeField]
    private Ease centerEase = Ease.OutQuad;

    [Header("Result")]
    [SerializeField]
    private int result;

    [Header("Events")]
    public UnityEvent<int> OnRollComplete;

    private bool isRolling;

    public int Result => result;
    public bool IsRolling => isRolling;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    public void Roll()
    {
        if (isRolling)
            return;

        StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        isRolling = true;
        result = 0;

        transform.DOKill();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.rotation = Random.rotation;

        Vector3 direction = new Vector3(
            Random.Range(-1f, 1f),
            1f,
            Random.Range(-1f, 1f)
        ).normalized;

        rb.AddForce(
            direction * rollForce,
            ForceMode.Impulse
        );

        rb.AddTorque(
            Random.insideUnitSphere * torqueForce,
            ForceMode.Impulse
        );

        yield return StartCoroutine(
            WaitForDiceToSlowDown()
        );

        yield return StartCoroutine(
            MoveDiceToCenter()
        );

        yield return StartCoroutine(
            WaitUntilStopped()
        );

        CalculateResult();

        isRolling = false;

        OnRollComplete?.Invoke(result);
    }

    private IEnumerator WaitForDiceToSlowDown()
    {
        while (true)
        {
            float velocity =
                rb.linearVelocity.magnitude;

            float angularVelocity =
                rb.angularVelocity.magnitude;

            if (velocity <= slowVelocity &&
                angularVelocity <= slowAngularVelocity)
            {
                break;
            }

            yield return null;
        }
    }

    // =========================================================
    // MOVE TO CENTER
    // =========================================================

    private IEnumerator MoveDiceToCenter()
    {
        if (diceCenter == null)
        {
            Debug.LogWarning(
                $"{name}: Dice Center is not assigned."
            );

            yield break;
        }

        Vector3 centerPosition =
            diceCenter.position;

        float distance =
            Vector3.Distance(
                transform.position,
                centerPosition
            );

        if (distance < centerDistance)
            yield break;

        rb.linearVelocity = Vector3.zero;

        rb.angularVelocity =
            rb.angularVelocity * 0.5f;

        rb.isKinematic = true;

        bool finished = false;

        transform.DOMove(
            centerPosition,
            centerDuration
        )
        .SetEase(centerEase)
        .OnComplete(() =>
        {
            finished = true;
        });

        while (!finished)
        {
            yield return null;
        }

        // Return to physics.
        rb.isKinematic = false;

        rb.linearVelocity = Vector3.zero;
    }

    private IEnumerator WaitUntilStopped()
    {
        float stillTime = 0f;

        while (true)
        {
            bool velocityStopped =
                rb.linearVelocity.magnitude <=
                stopVelocity;

            bool rotationStopped =
                rb.angularVelocity.magnitude <=
                stopAngularVelocity;

            if (velocityStopped &&
                rotationStopped)
            {
                stillTime += Time.deltaTime;

                if (stillTime >= requiredStillTime)
                    break;
            }
            else
            {
                stillTime = 0f;
            }

            yield return null;
        }
    }

    private void CalculateResult()
    {
        if (faces == null || faces.Length == 0)
        {
            Debug.LogError(
                $"{name}: No DiceFace objects assigned."
            );

            return;
        }

        float highestDot =
            -Mathf.Infinity;

        DiceFace topFace = null;

        foreach (DiceFace face in faces)
        {
            if (face == null)
                continue;

            if (face.faceDirection == null)
                continue;

            Vector3 faceNormal =
                face.faceDirection.forward;

            float dot =
                Vector3.Dot(
                    faceNormal,
                    Vector3.up
                );

            if (dot > highestDot)
            {
                highestDot = dot;
                topFace = face;
            }
        }

        if (topFace != null)
        {
            result = topFace.value;
        }
        else
        {
            Debug.LogError(
                $"{name}: Could not determine result."
            );
        }
    }

    public void ResetDice()
    {
        StopAllCoroutines();

        transform.DOKill();

        rb.isKinematic = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        result = 0;
        isRolling = false;
    }
}