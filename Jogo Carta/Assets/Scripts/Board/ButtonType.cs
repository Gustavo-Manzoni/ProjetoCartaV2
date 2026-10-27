using UnityEngine;
using UnityEngine.UI;

public class ButtonType : MonoBehaviour
{
   [SerializeField] ButtonTypes type;

    public ButtonTypes Type { get => type; set => type = value; }

    public Button MyButton { get; set;}
    void Awake()
    {
        MyButton = GetComponent<Button>();
    }
}
