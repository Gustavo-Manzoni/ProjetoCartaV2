using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.Events;
public class ModularQuest : MonoBehaviour
{
  
    [SerializeField] int needed;
    [SerializeField]UnityEvent onWin;
    int currentOrder;
    private void Awake()
    {
      
    }
    private void Start()
    {
      
        
    }
    public void MakeProgress() 
    {
        currentOrder++;
        AnimateText();
      
    
    }
    public bool CheckWin()
    {
        if (currentOrder >= needed){
           
            return true;
        }
        else
        {
            return false;
         }


     }
    private void AnimateText()
    {
       
    }


}