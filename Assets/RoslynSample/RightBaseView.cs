using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RightBaseView : BaseView
{

    protected override void OnAwake() 
    {
        base.OnAwake();
        Debug.Log("RightBaseView OnAwake");

    }


    [SkipBaseCall]
    protected override void Start()
    {
        Debug.Log("RightBaseView OnStart");
    }

}
