using UnityEngine;

// Best escape time and star rating, saved on this device (works in the browser too).
public static class SaveData
{
    const string BestTimeKey = "hh_best_time";
    const string BestStarsKey = "hh_best_stars";
    const string TutorialKey = "hh_tutorial_done";

    public const float FastEscapeSeconds = 240f; // 4:00 for the speed star

    public static bool HasBest => PlayerPrefs.HasKey(BestTimeKey);
    public static float BestTime => PlayerPrefs.GetFloat(BestTimeKey, 0f);
    public static int BestStars => PlayerPrefs.GetInt(BestStarsKey, 0);

    public static bool TutorialDone
    {
        get => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
        set { PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // 1 star for escaping, +1 for escaping under 4:00, +1 for never being spotted.
    public static int StarsFor(float timeUsed, int timesSpotted)
    {
        int stars = 1;
        if (timeUsed <= FastEscapeSeconds) stars++;
        if (timesSpotted == 0) stars++;
        return stars;
    }

    // Records a win. Returns true if it beat the saved best time.
    public static bool RecordWin(float timeUsed, int stars)
    {
        bool newBest = !HasBest || timeUsed < BestTime;
        if (newBest) PlayerPrefs.SetFloat(BestTimeKey, timeUsed);
        if (stars > BestStars) PlayerPrefs.SetInt(BestStarsKey, stars);
        PlayerPrefs.Save();
        return newBest;
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }
}
