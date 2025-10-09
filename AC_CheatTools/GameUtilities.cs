using AC.Scene.Explore;
using Character;

namespace CheatTools;

internal static class GameUtilities
{
    public const string GameProcessName = "SamabakeScramble";

    /// <summary>
    /// True in character maker, both in main menu and in-game maker.
    /// </summary>
    public static bool InsideMaker => CharacterCreation.HumanCustom.Initialized;


    /// <summary>
    /// Get a display name of the character. Only use in interface, not for keeping track of the character.
    /// If <paramref name="translated"/> is true and AutoTranslator is active, try to get a translated version of the name in current language. Otherwise, return the original name.
    /// </summary>
    public static string GetCharaName(this Actor chara, bool translated)
    {
        if(chara == null) return "";

        var fullname = chara.BaseData?.HumanData?.Parameter.GetCharaName(translated);
        if (!string.IsNullOrEmpty(fullname))
        {
            if (translated)
            {
                TranslationHelper.TryTranslate(fullname, out var translatedName);
                if (!string.IsNullOrEmpty(translatedName))
                    return translatedName;
            }
            return fullname;
        }
        return chara._charaFileName ?? chara.ToString();
    }
    public static string GetCharaName(this HumanData chara, bool translated)
    {
        if(chara == null) return "";

        var fullname = chara.Parameter?.GetCharaName(translated);
        if (!string.IsNullOrEmpty(fullname))
        {
            if (translated)
            {
                TranslationHelper.TryTranslate(fullname, out var translatedName);
                if (!string.IsNullOrEmpty(translatedName))
                    return translatedName;
            }
            return fullname;
        }
        return chara.CharaFileName ?? chara.ID.ToString();
    }

    /// <summary>
    /// Get a display name of the character. Only use in interface, not for keeping track of the character.
    /// If <paramref name="translated"/> is true and AutoTranslator is active, try to get a translated version of the name in current language. Otherwise, return the original name.
    /// </summary>
    public static string GetCharaName(this HumanDataParameter param, bool translated)
    {
        if(param == null) return "";

        var fullname = param.fullname;
        if (string.IsNullOrEmpty(fullname)) return "";

        if (translated)
        {
            TranslationHelper.TryTranslate(fullname, out var translatedName);
            if (!string.IsNullOrEmpty(translatedName))
                return translatedName;
        }
        return fullname;
    }
}
