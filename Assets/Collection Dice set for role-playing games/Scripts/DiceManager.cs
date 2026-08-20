using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DiceManager : MonoBehaviour
{
    public enum DiceType
    {
        D4 = 4,
        D6 = 6,
        D8 = 8,
        D10 = 10,
        D12 = 12,
        D20 = 20
    }

    [Header("Dice Selection")]
    [SerializeField]
    private DiceType selectedDice = DiceType.D6;

    
    [SerializeField]
    private Transform startingPos;

    [Header("Dice References")]
    [SerializeField]
    private DiceRoller d4;

    [SerializeField]
    private DiceRoller d6;

    [SerializeField]
    private DiceRoller d8;

    [SerializeField]
    private DiceRoller d10;

    [SerializeField]
    private DiceRoller d12;

    [SerializeField]
    private DiceRoller d20;

    [Header("UI")]

    [SerializeField]
    private TMP_Text resultText;

    [Header("Messages")]
    [SerializeField]
    private string defaultResultText = "Roll the Dice";

    [SerializeField]
    private string rollingText = "Rolling...";

    private DiceRoller currentDice;

    private void Start()
    {
        DisableDices();

        if (resultText != null)
        {
            resultText.text = defaultResultText;
        }
    }

    private void OnDestroy()
    {

        UnsubscribeFromDice();
    }

    public void SelectDiceDrop(int value)
    {
        if(currentDice != null)
            if (currentDice.IsRolling)
                return;
        
        DisableDices();
        switch (value)
        {
            case 0:
                selectedDice = DiceType.D4;
                d4.gameObject.SetActive(true);
                break;

            case 1:
                selectedDice = DiceType.D6;
                d6.gameObject.SetActive(true);
                break;

            case 2:
                selectedDice = DiceType.D8;
                d8.gameObject.SetActive(true);
                break;

            case 3:
                selectedDice = DiceType.D10;
                d10.gameObject.SetActive(true);
                break;

            case 4:
                selectedDice = DiceType.D12;
                d12.gameObject.SetActive(true);
                break;

            case 5:
                selectedDice = DiceType.D20;
                d20.gameObject.SetActive(true);
                break;

            default:
                Debug.LogError("Invalid dice dropdown value: " + value);
                return;
        }

        UpdateSelectedDice();
    }

    private void DisableDices()
    {
        d4.gameObject.SetActive(false);
        d6.gameObject.SetActive(false);
        d8.gameObject.SetActive(false);
        d10.gameObject.SetActive(false);
        d12.gameObject.SetActive(false);
        d20.gameObject.SetActive(false);
    }

    private void UpdateSelectedDice()
    {
        UnsubscribeFromDice();

        currentDice =
            GetDice(selectedDice);

        if (currentDice == null)
        {
            Debug.LogError(
                $"No dice assigned for {selectedDice}"
            );

            return;
        }

        

        currentDice.OnRollComplete.AddListener(
            OnDiceRollComplete
        );
        RollSelectedDice();
    }

    private DiceRoller GetDice(DiceType type)
    {
        switch (type)
        {
            case DiceType.D4:
                return d4;

            case DiceType.D6:
                return d6;

            case DiceType.D8:
                return d8;

            case DiceType.D10:
                return d10;

            case DiceType.D12:
                return d12;

            case DiceType.D20:
                return d20;
        }

        return null;
    }


    public void RollSelectedDice()
    {

        if (currentDice == null)
        {
            Debug.LogWarning(
                "No dice selected."
            );

            return;
        }

        currentDice.transform.localPosition = startingPos.localPosition;

        if (currentDice.IsRolling)
            return;

        if (resultText != null)
        {
            resultText.text = rollingText;
        }

        currentDice.Roll();
    }


    private void OnDiceRollComplete(int result)
    {
        if (resultText != null)
        {
            resultText.text =
                result.ToString();
        }

        Debug.Log(
            $"{selectedDice} Result: {result}"
        );
    }


    private void UnsubscribeFromDice()
    {
        if (currentDice == null)
            return;

    }


    public void SelectD4()
    {
        SelectDice(DiceType.D4);
    }

    public void SelectD6()
    {
        SelectDice(DiceType.D6);
    }

    public void SelectD8()
    {
        SelectDice(DiceType.D8);
    }

    public void SelectD10()
    {
        SelectDice(DiceType.D10);
    }

    public void SelectD12()
    {
        SelectDice(DiceType.D12);
    }

    public void SelectD20()
    {
        SelectDice(DiceType.D20);
    }

    public void SelectDice(DiceType type)
    {
        selectedDice = type;

        UpdateSelectedDice();
    }

    public int GetResult()
    {
        if (currentDice == null)
            return 0;

        return currentDice.Result;
    }

    public DiceType GetSelectedDice()
    {
        return selectedDice;
    }
}