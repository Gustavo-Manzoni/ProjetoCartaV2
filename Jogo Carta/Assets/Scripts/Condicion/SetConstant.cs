using UnityEngine;
using TriInspector;
public class SetConstant : MonoBehaviour
{
    [SerializeField] string key;

    [SerializeField] KeyTypes keyType;

    [ShowIf(nameof(IsBool))]
    [SerializeField] BoolCondition boolCondition;

    [ShowIf(nameof(IsInt))]
    [SerializeField] IntCondition intCondition;

    [ShowIf(nameof(IsInt))]
    [SerializeField] int value;

    [SerializeField] int resetValue;

    bool IsBool => keyType == KeyTypes.Bool;
    bool IsInt => keyType == KeyTypes.Int;

    public void SetValue() 
    {
        print($"Setting {key} to {value}");
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }

    public void ResetValue()
    {
        print($"Resetting {key} to {resetValue}");
        PlayerPrefs.SetInt(key, resetValue);
        PlayerPrefs.Save();
    }
}
