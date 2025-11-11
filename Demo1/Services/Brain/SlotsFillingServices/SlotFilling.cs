using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Twilio.TwiML.Voice;

///ASR Text → Normalization → Canonical Name → Slot Filling
namespace Demo1.Services.Brain.SlotsFillingServices;

public static class SlotFilling
{
    //  PROVIDERS FOR DB in FUETURE!
    //private static IMasterCatalog? _catalog;
    //private static IServiceLexicon? _lexicon;

    // 
    //public static void Configure(IMasterCatalog? catalog = null, IServiceLexicon? lexicon = null)
    //{
    //    _catalog = catalog;
    //    _lexicon = lexicon;
    //}

     #region Service and master data set up

    // List of available masters
    public static readonly string[] Masters = { "Nikita", "Emily", "Pablo" };

    // Duration of each service in minutes
    public static readonly Dictionary<string, int> ServiceDurationMin =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["Haircut"] = 45,
        ["Men haircut"] = 40,
        ["Women haircut"] = 60,
        ["Kids haircut"] = 30,
        ["Color"] = 90,
        ["Balayage"] = 120,
        ["Highlights"] = 90,
        ["Beard"] = 30
    };

    // Skills of each master
    public static readonly Dictionary<string, HashSet<string>> MasterSkills =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["Emily"] = new(StringComparer.OrdinalIgnoreCase) { "Haircut", "Women haircut", "Color", "Highlights", "Balayage", "Kids haircut" },
        ["Nikita"] = new(StringComparer.OrdinalIgnoreCase) { "Haircut", "Men haircut", "Beard", "Color", "Highlights", "Kids haircut" },
        ["Pablo"] = new(StringComparer.OrdinalIgnoreCase) { "Haircut", "Men haircut", "Beard", "Kids haircut" }
    };

    #endregion


    #region Regex & constants (precompiled)

    // Regex to normalize spaces
    private static readonly Regex RxSpaces = new("\\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Regex to catch common colloquial contractions
    private static readonly Regex RxColloqWanna = new(@"\bwanna\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqGonna = new(@"\bgonna\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqGotta = new(@"\bgotta\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqKinda = new(@"\bkinda\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqSorta = new(@"\bsorta\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqOutta = new(@"\boutta\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqDunno = new(@"\bdunno\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqLemme = new(@"\blemme\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqGimme = new(@"\bgimme\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqAint = new(@"\bain['’]?t\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqYall = new(@"\by['’]?all\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxColloqImma = new(@"\bimma\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // -------- Time --------

    // With explicit preposition ("at / в / a las / a la / sobre / around / about / к / около")
    // Named groups; supports 00–23 or 1–12 (+ optional am/pm with or without dots), : or .
    private static readonly Regex RxAtClock =
        new(@"\b(?:(?:at|around|about|в|к|около|a\s+las?|sobre)\s+)"
           + @"(?<hour>2[0-3]|[01]?\d|1[0-2])"
           + @"(?:[:\.](?<min>[0-5]\d))?"
           + @"\s*(?<ampm>a\.?m\.?|p\.?m\.?)?\b",
           RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Standalone time (e.g., "3", "3pm", "15:30", "7.05 p.m.")
    private static readonly Regex RxHourMinute =
        new(@"\b(?<hour>2[0-3]|[01]?\d|1[0-2])"
           + @"(?:[:\.](?<min>[0-5]\d))?"
           + @"\s*(?<ampm>a\.?m\.?|p\.?m\.?)?\b",
           RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Day words — NOTE: run on *normalized* text (mañana -> "manana")
    private static readonly Regex RxWordManana =
        new(@"\bmanana\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex RxWordHoy =
        new(@"\bhoy\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    // (Optional extras you likely want)
    private static readonly Regex RxWordTomorrow =
        new(@"\btomorrow\b|\bзавтра\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex RxWordToday =
        new(@"\btoday\b|\bсегодня\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // -------- Names --------

    // Accepts 1–3 tokens made of Unicode letters (\p{L}), apostrophes, and hyphens.
    // Examples matched: "I'm John", "my name is Mary Jane", "this is Jean-Luc", "it's O'Connor"
    private static readonly Regex RxNameEn =
        new(@"\b(?:i\s*am|i'?m|my\s+name\s+is|this\s+is|it\s+is|it'?s)\s+"
           + @"(?<name>\p{L}[\p{L}'\-]+(?:\s+\p{L}[\p{L}'\-]+){0,2})\b",
           RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // RU: "меня зовут Анна Смирнова", also allow "это Анна" / "я Анна"
    private static readonly Regex RxNameRu =
        new(@"\b(?:меня\s+зовут|это|я)\s+"
           + @"(?<name>[А-ЯЁA-Z][А-ЯЁA-Zа-яёa-z'-\-]+(?:\s+[А-ЯЁA-Zа-яёa-z'-\-]+){0,2})\b",
           RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Short “it’s <name>” (English) — kept for quick checks; RxNameEn already covers this.
    private static readonly Regex RxItsName =
        new(@"\bit'?s\s+(?<name>\p{L}[\p{L}'\-]+(?:\s+\p{L}[\p{L}'\-]+){0,2})\b",
           RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static Regex RxWithMaster = null!;
    private static Regex RxMasterAny = null!;

    #endregion

    // Master normalization 
    public static readonly Dictionary<string, string> MasterNormalize =
    new(StringComparer.OrdinalIgnoreCase)
    {
        // Emily
        ["emily"] = "Emily",
        ["emi"] = "Emily",
        ["emili"] = "Emily",
        ["emely"] = "Emily",
        ["эмилли"] = "Emily",
        ["эмили"] = "Emily",
        ["эмиля"] = "Emily",
        ["эмали"] = "Emily",
        ["эми"] = "Emily",

        // Nikita
        ["nikita"] = "Nikita",
        ["nik"] = "Nikita",
        ["niki"] = "Nikita",
        ["nick"] = "Nikita",
        ["никита"] = "Nikita",
        ["никитка"] = "Nikita",

        // Pablo
        ["pablo"] = "Pablo",
        ["пабло"] = "Pablo",
        ["пабла"] = "Pablo",
        ["павло"] = "Pablo",
    };

    /// <summary>
    /// his static constructor sets up compiled regular expressions for detecting master name
    /// variants in user input across multiple languages. It collects all canonical and normalized master names, builds
    /// language-specific patterns to match booking-related phrases, and configures timeouts to prevent excessive
    /// processing. These resources are used throughout the class to parse and extract relevant information from
    /// text.
    /// </summary>
    static SlotFilling()
    {
        static SlotFilling()
        {
            // Collect all master name variants:
            // canonical names + all normalized aliases/transliterations
            var allNameVariants = Masters
                .SelectMany(m => new[] { m }.Concat(
                    MasterNormalize.Where(kv => string.Equals(kv.Value, m, StringComparison.OrdinalIgnoreCase))
                                   .Select(kv => kv.Key)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(s => s.Length) // longest first → avoids partial matches
                .ToArray();

            // Build regex pattern for names (escaped for safety)
            var namesPattern = string.Join("|", allNameVariants.Select(Regex.Escape));

            // Soft letter boundaries, prevent matching inside words (e.g., "emilya" or "nikitaland")
            var BL = @"(?<=^|[^\p{L}])";
            var BR = @"(?=$|[^\p{L}])";

            // Flexible spacing and punctuation between tokens
            var SEP = @"[ \t\u00A0._\-—–]*";

            // Prefix expressions that indicate "with/to/for master <Name>" in EN/RU/ES
            var preName =
            @"(?:" +
                // EN: booking verbs + roles
                $@"(?:(?:with|w/|to|for|by|via){SEP}(?:the{SEP})?(?:master|stylist|barber|colorist|colourist|nail{SEP}tech|technician)?)|" +
                $@"(?:(?:book|schedule|set|make|put|reserve|fix|arrange){SEP}(?:me{SEP})?(?:with|w/|to)?{SEP}(?:the{SEP})?(?:master|stylist|barber|colorist|colourist|nail{SEP}tech|technician)?)|" +
                $@"(?:(?:appointment|appt){SEP}(?:with|w/|to){SEP}(?:the{SEP})?(?:master|stylist|barber|colorist|colourist|nail{SEP}tech|technician)?)|" +
                $@"(?:(?:see|prefer|request|change{SEP}to|switch{SEP}to){SEP}(?:the{SEP})?(?:master|stylist|barber|colorist|colourist|nail{SEP}tech|technician)?){SEP}with?)|" +

                // RU: "к/у/с мастеру", "запишите к ..."
                $@"(?:(?:к|у|с|со){SEP}(?:мастер(?:у|а)?|стилист(?:у|а)?|барбер(?:у|а)?|колорист(?:у|а)?|техник(?:у|а)?{SEP}по{SEP}ногтям)?)|" +
                $@"(?:(?:записать(?:ся)?|запишите|хочу|можно|нужна{SEP}запись|поставьте){SEP}(?:меня{SEP})?(?:к|к{SEP}мастеру|у|с))|" +
                $@"(?:(?:на{SEP}при(?:ё|е)м){SEP}(?:к|у|с))|" +

                // ES: "cita con", "reservar con", "al estilista"
                $@"(?:(?:con|a|al|a{SEP}la|para){SEP}(?:el|la)?{SEP}(?:maestro|estilista|barbero|colorista|t(?:e|é)cnico(?:{SEP}de{SEP}u(?:n|ñ)as)?)?)|" +
                $@"(?:(?:reservar|agendar|poner|hacer){SEP}(?:me{SEP})?(?:una{SEP})?(?:cita|turno)?{SEP}(?:con|a|al|para))|" +
                $@"(?:(?:cita|turno){SEP}(?:con|a|al){SEP}(?:el|la)?{SEP}(?:estilista|barbero|colorista|t(?:e|é)cnico(?:{SEP}de{SEP}u(?:n|ñ)as)?)?)" +
            @")";

            // Small timeout prevents catastrophic backtracking in noisy input
            var timeout = TimeSpan.FromMilliseconds(150);

            // Pattern: "prefix + Name"
            RxWithMaster = new Regex(
                $@"{BL}{preName}{SEP}({namesPattern}){BR}",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                timeout);

            // Pattern: "bare Name" (fallback if context missing)
            RxMasterAny = new Regex(
                $@"{BL}({namesPattern}){BR}",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                timeout);
        }
    }

    /// <summary>
    ///  Lowercase, strip diacritics, collapse spaces, and normalize common colloquialisms.
    /// </summary>
    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        // 1) Lowercase & remove diacritics (accents)
        var lower = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        
        var sb = new StringBuilder(lower.Length);

        foreach (var ch in lower)
        {
            // Removes the accent
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        // Combines characters back into standard final letters
        var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);

        // 2) Normalize colloquial speech
        var t = noDiacritics;

        t = RxColloqWanna.Replace(t, "want to");
        t = RxColloqGonna.Replace(t, "going to");
        t = RxColloqGotta.Replace(t, "have to");
        t = RxColloqKinda.Replace(t, "kind of");
        t = RxColloqSorta.Replace(t, "sort of");

        t = RxColloqOutta.Replace(t, "out of");
        t = RxColloqDunno.Replace(t, "do not know");
        t = RxColloqLemme.Replace(t, "let me");
        t = RxColloqGimme.Replace(t, "give me");
        t = RxColloqAint.Replace(t, "is not");
        t = RxColloqYall.Replace(t, "you all");
        t = RxColloqImma.Replace(t, "i am going to");

        // 3) Collapse extra whitespace
        return RxSpaces.Replace(t, " ").Trim();
    }

    // Canonical services & synonyms 
    public static readonly Dictionary<string, string> ServiceSynonyms =
    new(StringComparer.OrdinalIgnoreCase)
    {
        // generic
        ["haircut"] = "Haircut",

        // men
        ["men haircut"] = "Men haircut",
        ["men's haircut"] = "Men haircut",
        ["mens haircut"] = "Men haircut",
        ["men cut"] = "Men haircut",
        ["male haircut"] = "Men haircut",
        ["gentlemen haircut"] = "Men haircut",
        ["caballero corte"] = "Men haircut",
        ["мужская стрижка"] = "Men haircut",
        ["стрижку мужскую"] = "Men haircut",
        ["мужскую стрижку"] = "Men haircut",
        ["стрижка для парня"] = "Men haircut",
        ["стрижка для мужчины"] = "Men haircut",

        // women
        ["womens haircut"] = "Women haircut",
        ["women haircut"] = "Women haircut",
        ["women's haircut"] = "Women haircut",
        ["women cut"] = "Women haircut",
        ["ladies cut"] = "Women haircut",
        ["lady haircut"] = "Women haircut",
        ["female haircut"] = "Women haircut",
        ["женская стрижка"] = "Women haircut",
        ["стрижку женскую"] = "Women haircut",
        ["стрижка для женщин"] = "Women haircut",
        ["стрижка для девушек"] = "Women haircut",
        ["стрижка для девушки"] = "Women haircut",
        ["стрижка для женщины"] = "Women haircut",
        ["женскую стрижку"] = "Women haircut",

        // KIDS — отдельный канон
        ["kids haircut"] = "Kids haircut",
        ["kid haircut"] = "Kids haircut",
        ["child haircut"] = "Kids haircut",
        ["children haircut"] = "Kids haircut",
        ["boy haircut"] = "Kids haircut",
        ["girl haircut"] = "Kids haircut",
        ["teen haircut"] = "Kids haircut",
        ["niño corte"] = "Kids haircut",
        ["niña corte"] = "Kids haircut",
        ["niños corte"] = "Kids haircut",
        ["детская стрижка"] = "Kids haircut",
        ["стрижка ребенка"] = "Kids haircut",
        ["стрижка ребёнка"] = "Kids haircut",
        ["стрижка мальчика"] = "Kids haircut",
        ["стрижка девочки"] = "Kids haircut",
        ["детскую стрижка"] = "Kids haircut",
        ["стрижка для ребенка"] = "Kids haircut",
        ["стрижка для мальчика"] = "Kids haircut",
        ["стрижка для девочки"] = "Kids haircut",
        ["для детей"] = "Kids haircut",

        // color
        ["color"] = "Color",
        ["colour"] = "Color",
        ["окрашивание"] = "Color",
        ["coloracion"] = "Color",

        // others
        ["balayage"] = "Balayage",
        ["highlights"] = "Highlights",
        ["lowlights"] = "Highlights",
        ["мелирование"] = "Highlights",

        ["manicure"] = "Manicure",
        ["маникюр"] = "Manicure",

        ["pedicure"] = "Pedicure",
        ["pedicura"] = "Pedicure",
        ["педикюр"] = "Pedicure",

        ["beard"] = "Beard",
        ["beard trim"] = "Beard",
        ["beard trimming"] = "Beard",
        ["борода"] = "Beard",
        ["стрижка бороды"] = "Beard",
        ["подравнивание бороды"] = "Beard",
        ["barba"] = "Beard",
    };

    // Tokens for each canonical service
    public static readonly Dictionary<string, string[]> CanonicalToTokens =
    new(StringComparer.OrdinalIgnoreCase)
    {
        // ——— GENERIC HAIRCUT ———
        ["Haircut"] = new[]
        {
            "haircut","cut","trim","bangs","corte",
            "стрижка","подстричь","подстричься","стригануть","сделать стрижку"
        },

        // ——— MEN ———
        ["Men haircut"] = new[]
        {
            // English
            "men","man's","mens","male","gentlemen","guy","boyfriend",
            // Spanish
            "caballero","hombre","varon","varón",
            // Russian
            "муж","мужик","мужской","мужская","парня","парень","для парня","для мужчины",
            "юноша","юноши","парикмахерская мужская"
        },

        // ——— WOMEN ———
        ["Women haircut"] = new[]
        {
            // English
            "women","woman","women's","womens","lady","ladies","female","girl","girlfriend",
            // Spanish
            "dama","mujer","señora","senora","chica",
            // Russian
            "жен","женщина","женской","женская","для неё","для нее",
            "девушка","девушки","леди"
        },

        // ——— KIDS ———
        ["Kids haircut"] = new[]
        {
            // English
            "kid","kids","child","children","boy","girl","teen","teenager",
            // Spanish
            "niño","niña","niños","ninos","hijo","hija",
            // Russian (root forms to match any case)
            "детск","ребён","ребен","мальчик","девочка","детям","для детей","школьник","садик"
        },

            // ——— COLOR ———
        ["Color"] = new[]
        {
            "color","colour","dye","toner","coloring","colored","paint",
            "окрашивание","покрасить","краситель","тонировка","coloracion","tintura"
        },

            // ——— BALAYAGE ———
        ["Balayage"] = new[]
        {
            "balayage","балаяж","балайяж"
        },

            // ——— HIGHLIGHTS / LOWLIGHTS ———
        ["Highlights"] = new[]
        {
            "highlights","highlight","lowlights","мелирование","мелира","блонд пряди"
        },
            // ——— BEARD ———
        ["Beard"] = new[]
        {
            "beard","beard trim","борода","барба","подравнять бороду","оформление бороды"
        }
    };


}

