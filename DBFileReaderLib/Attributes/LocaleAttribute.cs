using System;

namespace DBFileReaderLib.Attributes
{
    public class LocaleAttribute : Attribute
    {
        /// <summary>
        /// See https://wowdev.wiki/Localization
        /// </summary>
        public readonly int Locale;
        /// <summary>
        /// Number of available locales
        /// </summary>
        public readonly int LocaleCount;

        public LocaleAttribute(int locale, int localecount = 16)
        {
            Locale = locale;
            LocaleCount = localecount;
        }
    }
}
