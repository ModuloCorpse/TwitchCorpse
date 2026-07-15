namespace TwitchCorpse
{
    public static class Helper
    {
        //TODO improve escape characters
        public static string DecodeUnicode(string str) => str
            .Replace("\\u003e", ">")
            .Replace("\\u003c", "<")
            .Replace("\\u0026", "&");
        //System.Text.RegularExpressions.Regex.Unescape(text!);
    }
}
