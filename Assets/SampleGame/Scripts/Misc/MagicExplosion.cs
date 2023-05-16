using System;
using System.Collections;
using System.Collections.Generic;
using SynupsNetworking.core;
using SynupsNetworking.core.Attributes;
using SynupsNetworking.core.Enums;
using UnityEngine;

public class MagicExplosion : NetworkCallbacks
{

    public Light light;
    public AnimationCurve lightCurve;

    private void Start()
    {
        StartCoroutine(fadeLights());

    }

    private IEnumerator fadeLights()
    {
        float tc = 0;
        
        while (lightCurve.Evaluate(tc)>0)
        {
            light.intensity = lightCurve.Evaluate(tc);
            tc += Time.deltaTime;
            yield return null;
        }
    }
    
    
}
