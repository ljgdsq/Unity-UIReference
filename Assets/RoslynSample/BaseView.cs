using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseView : MonoBehaviour
{


    protected virtual void Awake()
    {
        OnAwake();
    }

    // Start is called before the first frame update
    protected virtual void Start()
    {
        OnStart();
    }

    // Update is called once per frame
    private void Update()
    {
        OnUpdate();
    }

    protected virtual void OnAwake(){}
    protected virtual void OnStart(){}
    protected virtual void OnUpdate(){}


}
