﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;

namespace Microsoft.DotNet.Cli
{
    internal static class UILanguageOverride
    {
        private const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);
        private const string VSLANG = nameof(VSLANG);
        private const string PreferredUILang = nameof(PreferredUILang);
        private static Encoding DefaultMultilingualEncoding = Encoding.UTF8; // We choose UTF8 as the default encoding as opposed to specific language encodings because it supports emojis & other chars in .NET.

        public static void Setup()
        {
            CultureInfo language = GetOverriddenUIOrSystemLanguage();
            if (language != null)
            {
                ApplyOverrideToCurrentProcess(language);
                FlowOverrideToChildProcesses(language);
            }
        }

        private static void ApplyOverrideToCurrentProcess(CultureInfo language)
        {

            CultureInfo.DefaultThreadCurrentCulture = language;
            CultureInfo.DefaultThreadCurrentUICulture = language;

            // The CurrentUICulture can be supposedly be left incorrectly unchanged even when DefaultThreadCurrentUICulture is changed:
            // https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.defaultthreadcurrentculture?redirectedfrom=MSDN&view=net-7.0#remarks
            CultureInfo.CurrentUICulture = language;

            if (OperatingSystem.IsWindows() && Environment.OSVersion.Version.Major >= 10) // Encoding is only an issue on Windows, UTF-8 is only officially supported on 10+.
            {
                Console.OutputEncoding = DefaultMultilingualEncoding;
                Console.InputEncoding = DefaultMultilingualEncoding; // Setting both encodings causes a change in the CHCP, making it so we dont need to P-Invoke ourselves.
                // If the InputEncoding is not set, the encoding will work in CMD but not in Powershell, as the raw CHCP page won't be changed.
            }
        }

        private static void FlowOverrideToChildProcesses(CultureInfo language)
        {
            // Do not override any environment variables that are already set as we do not want to clobber a more granular setting with our global setting.
            SetIfNotAlreadySet(DOTNET_CLI_UI_LANGUAGE, language.Name);
            SetIfNotAlreadySet(VSLANG, language.LCID); // for tools following VS guidelines to just work in CLI
            SetIfNotAlreadySet(PreferredUILang, language.Name); // for C#/VB targets that pass $(PreferredUILang) to compiler
        }

        /// <summary>
        /// Look first at UI Language Overrides. (DOTNET_CLI_UI_LANGUAGE and VSLANG).
        /// Then, see if the system language is set to a language besides English.
        /// </summary>
        /// <returns>The custom language (or not english language) that was set by the user.
        /// DOTNET_CLI_UI_LANGUAGE > VSLANG > System Language (Not English.) Returns null if none are set to another language.</returns>
        private static CultureInfo GetOverriddenUIOrSystemLanguage()
        {
            // DOTNET_CLI_UI_LANGUAGE=<culture name> is the main way for users to customize the CLI's UI language.
            string dotnetCliLanguage = Environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
            if (dotnetCliLanguage != null)
            {
                try
                {
                    return new CultureInfo(dotnetCliLanguage);
                }
                catch (CultureNotFoundException) { }
            }

            // VSLANG=<lcid> is set by VS and we respect that as well so that we will respect the VS 
            // language preference if we're invoked by VS. 
            string vsLang = Environment.GetEnvironmentVariable(VSLANG);
            if (vsLang != null && int.TryParse(vsLang, out int vsLcid))
            {
                try
                {
                    return new CultureInfo(vsLcid);
                }
                catch (ArgumentOutOfRangeException) { }
                catch (CultureNotFoundException) { }
            }

            if (!CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.InvariantCultureIgnoreCase))
            {
                return CultureInfo.InstalledUICulture;
            }

            return null;
        }

        private static void SetIfNotAlreadySet(string environmentVariableName, string value)
        {
            string currentValue = Environment.GetEnvironmentVariable(environmentVariableName);
            if (currentValue == null)
            {
                Environment.SetEnvironmentVariable(environmentVariableName, value);
            }
        }

        private static void SetIfNotAlreadySet(string environmentVariableName, int value)
        {
            SetIfNotAlreadySet(environmentVariableName, value.ToString());
        }
    }
}
