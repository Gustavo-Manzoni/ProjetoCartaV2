using System;
using UnityEngine;
using TriInspector;

public enum KeyTypes
{
    Bool, Int
}
public enum BoolCondition
{
    True, False
}
public enum IntCondition
{
    Equals, Greater, Less
}

[Serializable]
public class Condition
{
    [SerializeField] string key;

    [SerializeField] KeyTypes keyType;

    [ShowIf(nameof(IsBool))]
    [SerializeField] BoolCondition boolCondition;

    [ShowIf(nameof(IsInt))]
    [SerializeField] IntCondition intCondition;

    [ShowIf(nameof(IsInt))]
    [SerializeField] int value;

    bool IsBool => keyType == KeyTypes.Bool;
    bool IsInt => keyType == KeyTypes.Int;

    public bool CheckCondicion()
    {
        if(!PlayerPrefs.HasKey(key)){return false;}
        
        return keyType == KeyTypes.Bool ? CheckForBoolCondition() : CheckForIntCondition();
    }
    bool CheckForBoolCondition()
    {
        bool currentValue = PlayerPrefs.GetInt(key) == 0 ? false : true;
        bool targetValue = boolCondition == BoolCondition.True ? true : false;

        return currentValue == targetValue;
    }
    bool CheckForIntCondition()
    {
        int currentValue = PlayerPrefs.GetInt(key);

        switch (intCondition)
        {
            case IntCondition.Equals:
            return currentValue == value ? true : false;

            case IntCondition.Greater:
            return currentValue > value ? true : false;

            case IntCondition.Less:
            return currentValue < value ? true : false;
        }

        return false;

    }
}