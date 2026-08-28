using UnityEngine;

public static class PaymentProgressStore
{
    public static int Load(string key, int maxValue)
    {
        if (string.IsNullOrEmpty(key))
        {
            return 0;
        }

        return Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, Mathf.Max(0, maxValue));
    }

    public static void Save(string key, int value, int maxValue)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        PlayerPrefs.SetInt(key, Mathf.Clamp(value, 0, Mathf.Max(0, maxValue)));
        PlayerPrefs.Save();
    }

    public static void Clear(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
