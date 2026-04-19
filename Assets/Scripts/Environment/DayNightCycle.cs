// using UnityEngine;

// public class DayNightCycle : MonoBehaviour
// {
//     [Header("Time Settings")]
//     public float dayDurationInSeconds = 120f;
    
//     [Range(0f, 24f)]
//     public float currentTime = 12f;
    
//     public bool timeProgresses = true;
    
//     [Header("Sun/Moon Settings")]
//     public Light sunLight;
    
//     public Gradient sunColor;
    
//     public AnimationCurve sunIntensityCurve;
    
//     [Header("Ambient Settings")]
//     public Gradient ambientColor;
    
//     public Gradient fogColor;
    
//     private float timeMultiplier;

//     void Start()
//     {
//         timeMultiplier = 24f / dayDurationInSeconds;
//         SetupDefaults();
//     }

//     void Update()
//     {
//         if (timeProgresses)
//         {
//             currentTime += Time.deltaTime * timeMultiplier;
            
//             if (currentTime >= 24f)
//                 currentTime = 0f;
//         }
        
//         UpdateLighting();
//     }

//     void UpdateLighting()
//     {
//         float normalizedTime = currentTime / 24f;
        
//         if (sunLight != null)
//         {
//             float sunAngle = (currentTime - 6f) * 15f;
//             sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
            
//             sunLight.color = sunColor.Evaluate(normalizedTime);
//             sunLight.intensity = sunIntensityCurve.Evaluate(currentTime);
//         }
        
//         RenderSettings.ambientLight = ambientColor.Evaluate(normalizedTime);
        
//         RenderSettings.fogColor = fogColor.Evaluate(normalizedTime);
//     }

//     void SetupDefaults()
//     {
//         if (sunIntensityCurve == null || sunIntensityCurve.length == 0)
//         {
//             sunIntensityCurve = new AnimationCurve();
//             sunIntensityCurve.AddKey(0f, 0f);
//             sunIntensityCurve.AddKey(6f, 0.1f);
//             sunIntensityCurve.AddKey(12f, 1f);
//             sunIntensityCurve.AddKey(18f, 0.1f);
//             sunIntensityCurve.AddKey(24f, 0f);
//         }
        
//         if (sunColor == null || sunColor.colorKeys.Length == 0)
//         {
//             sunColor = new Gradient();
//             GradientColorKey[] colorKeys = new GradientColorKey[5];
//             colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0f);
//             colorKeys[1] = new GradientColorKey(new Color(1f, 0.6f, 0.4f), 0.25f);
//             colorKeys[2] = new GradientColorKey(new Color(1f, 0.95f, 0.9f), 0.5f);
//             colorKeys[3] = new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.75f);
//             colorKeys[4] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1f);
            
//             GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
//             alphaKeys[0] = new GradientAlphaKey(1f, 0f);
//             alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
//             sunColor.SetKeys(colorKeys, alphaKeys);
//         }
        
//         if (ambientColor == null || ambientColor.colorKeys.Length == 0)
//         {
//             ambientColor = new Gradient();
//             GradientColorKey[] colorKeys = new GradientColorKey[3];
//             colorKeys[0] = new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 0f);
//             colorKeys[1] = new GradientColorKey(new Color(0.5f, 0.5f, 0.55f), 0.5f);
//             colorKeys[2] = new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 1f);
            
//             GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
//             alphaKeys[0] = new GradientAlphaKey(1f, 0f);
//             alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
//             ambientColor.SetKeys(colorKeys, alphaKeys);
//         }
        
//         if (fogColor == null || fogColor.colorKeys.Length == 0)
//         {
//             fogColor = new Gradient();
//             GradientColorKey[] colorKeys = new GradientColorKey[3];
//             colorKeys[0] = new GradientColorKey(new Color(0.1f, 0.1f, 0.15f), 0f);  
//             colorKeys[1] = new GradientColorKey(new Color(0.6f, 0.7f, 0.8f), 0.5f); 
//             colorKeys[2] = new GradientColorKey(new Color(0.1f, 0.1f, 0.15f), 1f);  
            
//             GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
//             alphaKeys[0] = new GradientAlphaKey(1f, 0f);
//             alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
//             fogColor.SetKeys(colorKeys, alphaKeys);
//         }
//     }
    
//     // Public method to set time directly
//     public void SetTime(float hour)
//     {
//         currentTime = Mathf.Clamp(hour, 0f, 24f);
//     }
// }