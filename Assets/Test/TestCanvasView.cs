using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(ReferenceHolder))]
public partial class TestanvasView : ViewBase
{
    protected override void OnViewAwake()
    {
        base.OnViewAwake();
             this.button.onClick.AddListener(() =>
        {
            Debug.Log("c");
        });
    }
}
