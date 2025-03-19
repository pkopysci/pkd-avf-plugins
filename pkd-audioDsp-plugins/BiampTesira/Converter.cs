using pkd_common_utils.Logging;

namespace BiampTesira;

internal static class Converter
{
    public static int ConvertToPercent(int dbLevel, int lvlMin, int lvlMax)
    {
        int rawVal;
        if (dbLevel < lvlMin)
        {
            rawVal = lvlMin;
        }
        else if (dbLevel > lvlMax)
        {
            rawVal = lvlMax;
        }
        else
        {
            rawVal = dbLevel;
        }

        const int newRange = 100;
        return (rawVal - lvlMin) * newRange / (lvlMax - lvlMin);
    }
    
    public static float ConvertToDb(int percent, int lvlMin, int lvlMax)
    {
        var rawVal = percent switch
        {
            < 0 => 0,
            > 100 => 100,
            _ => percent
        };

        float newRange = lvlMax - lvlMin;
        return ((rawVal * newRange) / 100) + lvlMin;
    }
    
    public static int FloatToPercent(float dbLevel, int lvlMin, int lvlMax)
    {
        Logger.Debug("BiampTesira.Converter.FloatToPercent({0}, {1}, {2})", dbLevel, lvlMin, lvlMax);

        double rawVal;
        if (dbLevel < lvlMin)
        {
            rawVal = lvlMin;
        }
        else if (dbLevel > lvlMax)
        {
            rawVal = lvlMax;
        }
        else
        {
            rawVal = dbLevel;
        }

        rawVal = Math.Round(rawVal, 0);
        const int newRange = 100;
        return (int)(rawVal - lvlMin) * newRange / (lvlMax - lvlMin);
    }
}