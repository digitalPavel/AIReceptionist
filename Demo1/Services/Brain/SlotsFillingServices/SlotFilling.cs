using Demo1.Models;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Twilio.Jwt.AccessToken;
using Twilio.TwiML.Voice;

///ASR Text → Normalization → Canonical Name → Slot Filling
namespace Demo1.Services.Brain.SlotsFillingServices;

public static class SlotFilling
{
    //  PROVIDERS FOR DB in FEUTURE!
    // private static IMasterCatalog? _catalog;
    //private static IServiceLexicon? _lexicon;

    // 
    //public static void Configure(IMasterCatalog? catalog = null, IServiceLexicon? lexicon = null)
    //{
    //    _catalog = catalog;
    //    _lexicon = lexicon;
    //}

    #region Service and master data set up ---------------------------------------------------------------------------------------

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

    #endregion -------------------------------------------------------------------------------------------------------


    #region Regex & constants (precompiled) --------------------------------------------------------------------------

    // Regex to detect "no preference" for master 
    private static readonly Regex RxNoMasterPreference =
    new(
        @"\b(" +
        // ---------- ENGLISH ----------
        @"any(?:one)?(?: stylist| master| barber| colorist)?(?: is)? (?:fine|ok|okay|good)" +  // "any stylist is fine"
        @"|any(?:one)?(?: works)?" +                                                          // "any", "anyone", "anyone works"
        @"|no preference" +
        @"|doesn'?t matter" +
        @"|i don'?t care" +
        @"|dont care" +
        @"|whatever" +
        @"|whoever" +
        @"|either is fine" +
        @"|no matter who" +
        @"|you can choose" +
        @"|you choose" +
        @"|up to you" +
        @"|surprise me" +

        // ---------- RUSSIAN ----------
        @"|любой(?: мастер| стилист| парикмахер)?" +                                          // "любой мастер/стилист..."
        @"|любая" +
        @"|любые" +
        @"|без разницы" +
        @"|мне без разницы" +
        @"|все равно" +
        @"|всё равно" +
        @"|мне все равно" +
        @"|мне всё равно" +
        @"|как угодно" +
        @"|мне не важно" +
        @"|не важно" +
        @"|пофиг" +
        @"|по барабану" +

        // ---------- SPANISH (без акцентов, т.к. Normalize всё убирает) ----------
        @"|cualquiera(?: esta bien)?" +                                                       // "cualquiera", "cualquiera está bien"
        @"|me da igual" +
        @"|me es indiferente" +
        @"|no me importa" +
        @"|no tengo preferencia" +
        @"|sin preferencia" +
        @"|como sea" +
        @"|lo que sea" +
        @"|el que sea" +
        @"|la que sea" +
        @")\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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

    #endregion ---------------------------------------------------------------------------------------

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

    #region Normalization Helpers ---------------------------------------------------------------------------------

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

    /// <summary>
    /// A whole word match requires that the word is not immediately preceded or followed by a letter
    /// or digit. For example, searching for "cat" in "concatenate" returns false, but searching for "cat" in "the cat
    /// sat" returns true
    /// </summary>
    private static bool ContainsWordFast(string text, string word)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // 1) Find first occurrence (case-insensitive)
        int idx = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);

        // 2) Check word boundaries
        while (idx >= 0)
        {
            // Check left word boundary
            bool leftOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);

            // Check right word boundary
            int end = idx + word.Length;
            bool rightOk = end == text.Length || !char.IsLetterOrDigit(text[end]);

            // If both boundaries are ok, we found a whole word match and return true
            if (leftOk && rightOk) return true;

            // Otherwise, continue searching
            idx = text.IndexOf(word, idx + 1, StringComparison.Ordinal);
        }
        // No whole word match found
        return false;
    }

    #endregion Normalization Helpers ---------------------------------------------------------------------------------

    #region Canonical services & synonyms ----------------------------------------------------------------------------

    // Service synonyms
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

    // Normalized synonyms to the common format for fast lookup and resilience 
    public static readonly Dictionary<string, string> SynonymsNormalize =
        ServiceSynonyms.ToDictionary(kv => Normalize(kv.Key), kv => kv.Value, StringComparer.Ordinal);

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

    #endregion Canonical services & synonyms ----------------------------------------------------------------------------

    #region Gender specialization (Adults only) -------------------------------------------------------------------------

    private static readonly string[] MenTokens =
    {
        // English
        "man", "men", "man's", "men's",
        "male", "males",
        "gentleman", "gentlemen", "gent",
        "guy", "guys",
        "boy", "boys",
        "lad", "lads",

        // Spanish
        "hombre", "hombres",
        "caballero", "caballeros",
        "varon", "varones",      // varón
        "senor", "senores",      // señor
        "chico", "chicos",
        "nino", "ninos",         // niño / niños

        // Russian (номинатив и самые частые формы)
        "мужчина", "мужчины",
        "парень", "парни",
        "юноша", "юноши",
        "мужик", "мужики"
    };

    private static readonly string[] WomenTokens =
    {
        // English
        "woman", "women", "woman's", "women's",
        "female", "females",
        "lady", "ladies",
        "girl", "girls",
        "gal", "gals",

        // Spanish
        "mujer", "mujeres",
        "dama", "damas",
        "senora", "senoras",         // señora
        "senorita", "senoritas",     // señorita
        "chica", "chicas",
        "nina", "ninas",             // niña / niñas

        // Russian
        "женщина", "женщины",
        "девушка", "девушки",
        "дама", "дамы",
        "леди",                      // заимствованное, часто в beauty-контексте
        "девочка", "девочки"
    };

    private static readonly string[] MenRel =
    {
        // Partners
        "husband", "boyfriend", "fiance",
        "esposo", "novio",

        // Relatives
        "father", "dad", "daddy", "stepfather",
        "son", "sons",
        "brother", "bro", "bros",
        "grandfather", "grandpa", "granddad", "granddaddy",
        "uncle",
        "nephew",
        "padre", "papa",
        "hijo", "hijos",
        "hermano", "hermanos",
        "abuelo", "abuelos",
        "tio", "tios",
        "sobrino",

        // Friends
        "friend", "buddy", "pal", "dude", "homie", "mate",
        "amigo", "amigos"

        // RU
        ,"муж", "парень", "жених",
        "папа", "отец", "батя", "батяня",
        "сын", "сыночек",
        "брат", "братик",
        "дед", "дедушка", "дедуля",
        "дядя",
        "племянник",
        "друг", "дружище", "приятель"
    };

    private static readonly string[] WomenRel =
    {
        // Partners
        "wife", "girlfriend", "fiancee",
        "esposa", "novia",

        // Relatives
        "mother", "mom", "mommy", "stepmother",
        "daughter", "daughters",
        "sister", "sis",
        "grandmother", "grandma", "granny", "nana",
        "aunt",
        "niece",
        "madre", "mama",
        "hija", "hijas",
        "hermana", "hermanas",
        "abuela", "abuelas",
        "tia", "tias",
        "sobrina",

        // Friends
        "friend", "girlfriend", "bestie",
        "amiga", "amigas",
        "lady", "miss", "ms", "mrs",
        "senora", "señora", "senorita", "señorita",

        // RU
        "жена", "невеста",
        "мама", "мамочка", "мамуля",
        "дочь", "дочка",
        "сестра", "сестрёнка", "сестричка",
        "бабушка", "бабуля", "бабка",
        "тетя", "тётя",
        "племянница",
        "подруга", "лучшая подруга"
    };

    private static readonly string[] MenPhraseTokens =
    {
        "for him",
        "for my husband",
        "for my boyfriend",
        "for my son",
        "for my brother",
        "for my dad",
        "for my father",
        "for my grandpa",

        "para el",
        "para mi esposo",
        "para mi novio",
        "para mi hijo",
        "para mi hermano",
        "para mi papa",
        "para mi padre",
        "para mi abuelo",

        "для него",
        "для моего мужа",
        "для парня",
        "для моего сына",
        "для моего брата",
        "для моего папы",
        "для моего отца",
        "для моего дедушки"
    };

    private static readonly string[] WomenPhraseTokens =
    {
        "for her",
        "for my wife",
        "for my girlfriend",
        "for my daughter",
        "for my sister",
        "for my mom",
        "for my mother",
        "for my grandma",

        "para ella",
        "para mi esposa",
        "para mi novia",
        "para mi hija",
        "para mi hermana",
        "para mi mama",
        "para mi madre",
        "para mi abuela",

        "для нее",
        "для неё",
        "для моей жены",
        "для моей девушки",
        "для моей дочери",
        "для моей дочки",
        "для моей сестры",
        "для моей мамы",
        "для моей матери",
        "для моей бабушки"
    };

    private static readonly string[] RussianMenRoots =
    {
        "муж",   // мужчина, мужской, мужу, мужа…
        "парн"   // парень, парня, парню…
    };

    private static readonly string[] RussianWomenRoots =
    {
        "жен",      // женщина, женщины, женский…
        "девуш"     // девушка, девушке…
    };

    private static readonly string[] RussianPronounPhrases =
    {
        "для него",
        "для нее",
        "для неё"
    };


    /// <summary>
    /// This method analyzes the input text for the presence of masculine or feminine tokens, phrases, and
    /// roots in multiple languages. If both masculine and feminine indicators are found, or if neither is found, the
    /// method returns <see cref="ServiceGender.Unisex"/>
    /// </summary>
    public static ServiceGender TryInferServiceGender(string text)
    {
        var t = Normalize(text);

        bool man =
            MenTokens.Any(tok => ContainsWordFast(t, tok)) ||
            MenRel.Any(tok => ContainsWordFast(t, tok)) ||
            MenPhraseTokens.Any(p => t.Contains(p)) ||  
            RussianMenRoots.Any(root => t.Contains(root));

        bool woman =
            WomenTokens.Any(tok => ContainsWordFast(t, tok)) ||
            WomenRel.Any(tok => ContainsWordFast(t, tok)) ||
            WomenPhraseTokens.Any(p => t.Contains(p)) ||
            RussianWomenRoots.Any(root => t.Contains(root)) ||
            RussianPronounPhrases.Any(p => t.Contains(p));

        // If both or neither detected, return Unisex
        if (man ^ woman)
            return man ? ServiceGender.Man : ServiceGender.Woman;

        return ServiceGender.Unknown;
    }

    /// <summary>
    /// This method is typically used to decide if additional clarification is needed from the user
    /// when booking a haircut, especially when the service is not gender-specific and the user's gender cannot be
    /// determined from the provided information
    /// </summary>
    public static bool ShouldAskGenderForHaircut(string text, IEnumerable<string> services)
    {
        if(services is null)
            return false;

        // Convert to list for multiple enumerations
        var list = services as IList<string> ?? services.ToList();

        // 1) If no generic haircut, no need to ask
        bool hasGenericHaircut = list.Any(s =>
            s.Equals("Haircut", StringComparison.OrdinalIgnoreCase));

        if (!hasGenericHaircut)
            return false;

        // 2) Try identify gender
        var g = TryInferServiceGender(text);

        // 3) Ask only if unknown
        return g == ServiceGender.Unknown;
    }

    /// <summary>
    /// Replaces a generic "Haircut" with a gender-specific version ("Men haircut" / "Women haircut") 
    /// when gender is known. If gender is unknown, returns the list unchanged.
    /// </summary>
    public static List<string> MapHaircutByGender(IEnumerable<string> services, ServiceGender g)
    {
        var list = services.ToList();

        // Return list if gender unknown
        if (g == ServiceGender.Unknown)
            return list;

        // Replace generic “Haircut” with a gender - specific variant(“Men haircut” / “Women haircut”)
        // only when gender is known. Other services (e.g., "Kids haircut") remain untouched.  
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Equals("Haircut", StringComparison.OrdinalIgnoreCase))
                list[i] = g == ServiceGender.Man ? "Men haircut" : "Women haircut";         
        }
        return list;
    }

    #endregion Gender specialization (Adults only) -------------------------------------------------------------------------

    #region Group / Party (count + kind) -----------------------------------------------------------------------------------

    private static readonly Dictionary<string, int> NumWordsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,

        ["couple"] = 2,
        ["both"] = 2,
        ["pair"] = 2,
        ["duo"] = 2,
        ["twosome"] = 2,   
    };

    private static readonly Dictionary<string, int> NumWordsRu = new(StringComparer.OrdinalIgnoreCase)
    {
        ["один"] = 1,
        ["одного"] = 1,
        ["одна"] = 1,
        ["одной"] = 1,
        ["одну"] = 1,
        ["одиночка"] = 1, 

        ["двое"] = 2,
        ["двоих"] = 2,
        ["двум"] = 2,
        ["двумя"] = 2,
        ["два"] = 2,
        ["двух"] = 2,
        ["пара"] = 2,
        ["парочка"] = 2,
        ["оба"] = 2,
        ["обоих"] = 2,
        ["обе"] = 2,
        ["обеих"] = 2,

        ["три"] = 3,
        ["трех"] = 3,
        ["трёх"] = 3,
        ["трое"] = 3,
        ["троих"] = 3,
        ["троим"] = 3,

        ["четыре"] = 4,
        ["четверо"] = 4,
        ["четверых"] = 4,

        ["пять"] = 5,
        ["пятеро"] = 5,
        ["пятерых"] = 5,

        ["шесть"] = 6,
        ["семь"] = 7,
        ["восемь"] = 8,
        ["девять"] = 9,
        ["десять"] = 10,
    };

    private static readonly Dictionary<string, int> NumWordsEs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["uno"] = 1,
        ["una"] = 1,

        ["dos"] = 2,
        ["ambos"] = 2,
        ["ambas"] = 2,
        ["par"] = 2,
        ["pareja"] = 2,  

        ["tres"] = 3,
        ["cuatro"] = 4,
        ["cinco"] = 5,
        ["seis"] = 6,
        ["siete"] = 7,
        ["ocho"] = 8,
        ["nueve"] = 9,
        ["diez"] = 10,
    };

    private static readonly string[] KidsGroupTokens =
    {
       // ========= ENGLISH =========
        "kid", "kids",
        "child", "children",
        "baby", "babies",
        "newborn", "newborns",
        "infant", "infants",
        "toddler", "toddlers",
        "boy", "boys",
        "girl", "girls",
        "teen", "teens",
        "teenager", "teenagers",
        "son", "sons",
        "daughter", "daughters",

        // ========= SPANISH (NO ACCENTS: Normalize() уже убирает их из текста) =========
        "nino", "nina", "ninos", "ninas",
        "hijo", "hija", "hijos", "hijas",
        "bebe", "bebes",
        "pequeno", "pequena", "pequenos", "pequenas",
        "chico", "chica", "chicos", "chicas",
        "nene", "nena", "nenes", "nenas",
        "menor", "menores",
        "adolescente", "adolescentes",

        // ========= RUSSIAN =========
        "детей", "детям", "дети",
        "ребенок", "ребёнок", "ребенка", "ребёнка", "ребенку", "ребёнку",
        "ребят", "ребята",
        "малыш", "малыша", "малышу", "малыши", "малышам",
        "младенец", "младенца", "младенцу", "младенцы",
        "грудничок", "грудничка", "грудничку", "груднички",
        "мальчик", "мальчика", "мальчику", "мальчики",
        "девочка", "девочки", "девочкам",
        "сын", "сына", "сыну", "сыновья",
        "дочь", "дочери", "дочкам",
        "школьник", "школьника", "школьники", "школьникам",
        "подросток", "подростка", "подростки"
    };

    private static readonly string[] AdultTokens =
    {
         // ========= ENGLISH =========
        "adult", "adults",
        "we", "both of us", "all of us",

        "man", "men",
        "woman", "women",

        "wife", "husband",
        "girlfriend", "boyfriend",
        "partner", "partners",

        "mom", "mother",
        "dad", "father",
        "parent", "parents",

        "friend", "friends",
        "buddy", "buddies",

        // ========= SPANISH (NO ACCENTS IN TOKENS) =========
        "adulto", "adultos", "adulta", "adultas",
        "somos", "nosotros", "nosotras",
        "pareja", "parejas",

        "hombre", "hombres",
        "mujer", "mujeres",

        "esposo", "esposa",
        "novio", "novia",
        "novios", "novias",

        "padre", "padres",
        "madre",
        "mis padres",

        "amigo", "amigos",
        "amiga", "amigas",

        // ========= RUSSIAN =========
        "мы", "мы с",
        "взрослый", "взрослые",

        "мужчина", "мужчины",
        "женщина", "женщины",

        "жена", "муж",
        "девушка", "парень",
        "супруг", "супруга",

        "мама", "мамы", "маме", "маму",
        "папа", "папы", "папе", "папу",
        "родитель", "родители",

        "друг", "друзья",
        "подруга", "подруги",
        "приятель", "приятели"
    };

    /// <summary>
    /// Tries to extract party size (1..10) and kind (Adults / Kids / Mixed) from the input text.
    /// </summary>
    public static (int? size, string kind, string reason) TryExtractParty(string text)
    {
        var t = Normalize(text);

        // 1) MSRT как первый источник
        int? size = RecognizePartySizeWithMsrt(text);
        string kind = "Unknown";
        var reasons = new List<string>();

        if (size is int ps0)
            reasons.Add($"msrt={ps0}");

        // Локальный компаратор для строк
        static bool Eq(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // ===== Локальный хелпер: попытаться распарсить число из токена (цифра или слово) =====
        bool TryParsePartyNumberToken(string token, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            token = token.Trim().ToLowerInvariant();

            // 1) Цифры 1..10
            if (int.TryParse(token, out var n) && n >= 1 && n <= 10)
            {
                value = n;
                return true;
            }

            // 2) По словарям числительных (твои словари)
            if (NumWordsEn.TryGetValue(token, out value))
                return true;
            if (NumWordsRu.TryGetValue(token, out value))
                return true;
            if (NumWordsEs.TryGetValue(token, out value))
                return true;

            // 3) Доп. русские формы, которых нет в словаре, но полезны в mixed-паттернах
            switch (token)
            {
                case "трое":
                case "троих":
                    value = 3; return true;
                case "четверо":
                case "четверых":
                    value = 4; return true;
                case "пятеро":
                case "пятерых":
                    value = 5; return true;
                case "шестеро":
                case "шестерых":
                    value = 6; return true;
                case "семеро":
                case "семерых":
                    value = 7; return true;
                case "восьмеро":
                case "восьмерых":
                    value = 8; return true;
                case "девятеро":
                case "девятерых":
                    value = 9; return true;
                case "десятеро":
                case "десятерых":
                    value = 10; return true;
            }

            return false;
        }

        // ===== 2) Цифры (простой кейс: отдельные числа 1..10) =====
        var mDigits = Regex.Matches(t, @"\b(\d{1,2})\b");
        foreach (Match m in mDigits)
        {
            if (int.TryParse(m.Value, out var n) && n >= 1 && n <= 10)
            {
                size = Math.Max(size ?? 0, n);
                reasons.Add($"digits={n}");
            }
        }

        // ===== 3) Словесные числительные (en/ru/es) — общий максимум =====
        foreach (var kv in NumWordsEn)
            if (ContainsWordFast(t, kv.Key))
            {
                size = Math.Max(size ?? 0, kv.Value);
                reasons.Add($"en={kv.Key}:{kv.Value}");
            }

        foreach (var kv in NumWordsRu)
            if (ContainsWordFast(t, kv.Key))
            {
                size = Math.Max(size ?? 0, kv.Value);
                reasons.Add($"ru={kv.Key}:{kv.Value}");
            }

        foreach (var kv in NumWordsEs)
            if (ContainsWordFast(t, kv.Key))
            {
                size = Math.Max(size ?? 0, kv.Value);
                reasons.Add($"es={kv.Key}:{kv.Value}");
            }

        // ===== 4) Явные паттерны "X adults + Y kids" (EN / ES / RU, цифры и слова) =====
        int? mixedTotal = null;

        void TryApplyMixedPattern(Match m)
        {
            if (!m.Success) return;

            var gA = m.Groups["a"];
            var gK = m.Groups["k"];
            if (!gA.Success || !gK.Success) return;

            if (!TryParsePartyNumberToken(gA.Value, out var a)) return;
            if (!TryParsePartyNumberToken(gK.Value, out var k)) return;

            var total = a + k;
            if (total < 1 || total > 10) return;

            mixedTotal = Math.Max(mixedTotal ?? 0, total);
            reasons.Add($"adults+kids={a}+{k}->{total}");
        }

        // Паттерны словесных числительных (с учётом твоих словарей)
        const string EnNumWordPattern =
            "one|two|three|four|five|six|seven|eight|nine|ten|couple|both|pair";
        const string RuNumWordPattern =
            "один|одного|одна|двое|двоих|два|двух|три|трех|трёх|трое|троих|четыре|четверо|четверых|пять|шесть|семь|семеро|восемь|восьмеро|девять|девятеро|десять|десятеро|оба|обоих";
        const string EsNumWordPattern =
            "uno|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|ambos|par";

        // EN: "3 adults ... 2 kids" и "two kids ... three adults"
        var mixEn1 = Regex.Match(
            t,
            @"\b(?<a>\d{1,2}|" + EnNumWordPattern + @")\s+(?:adult(?:s)?|grown[- ]ups?)\b.*\b(?<k>\d{1,2}|" +
            EnNumWordPattern + @")\s+(?:kid(?:s)?|child(?:ren)?|children)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        var mixEn2 = Regex.Match(
            t,
            @"\b(?<k>\d{1,2}|" + EnNumWordPattern + @")\s+(?:kid(?:s)?|child(?:ren)?|children)\b.*\b(?<a>\d{1,2}|" +
            EnNumWordPattern + @")\s+(?:adult(?:s)?|grown[- ]ups?)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        TryApplyMixedPattern(mixEn1);
        TryApplyMixedPattern(mixEn2);

        // ES: "3 adultos ... 2 niños" / "dos niños ... tres adultos"
        // t уже нормализован: "niños" -> "ninos"
        var mixEs1 = Regex.Match(
            t,
            @"\b(?<a>\d{1,2}|" + EsNumWordPattern +
            @")\s+adultos?\b.*\b(?<k>\d{1,2}|" + EsNumWordPattern +
            @")\s+ninos?\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        var mixEs2 = Regex.Match(
            t,
            @"\b(?<k>\d{1,2}|" + EsNumWordPattern +
            @")\s+ninos?\b.*\b(?<a>\d{1,2}|" + EsNumWordPattern +
            @")\s+adultos?\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        TryApplyMixedPattern(mixEs1);
        TryApplyMixedPattern(mixEs2);

        // RU: "3 взрослых ... 2 детей" / "двое детей ... трое взрослых"
        var mixRu1 = Regex.Match(
            t,
            @"\b(?<a>\d{1,2}|" + RuNumWordPattern +
            @")\s+взросл\w*\b.*\b(?<k>\d{1,2}|" + RuNumWordPattern +
            @")\s+дет(?:ей|и|ям|ят|ок)?\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        var mixRu2 = Regex.Match(
            t,
            @"\b(?<k>\d{1,2}|" + RuNumWordPattern +
            @")\s+дет(?:ей|и|ям|ят|ок)?\b.*\b(?<a>\d{1,2}|" + RuNumWordPattern +
            @")\s+взросл\w*\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        TryApplyMixedPattern(mixRu1);
        TryApplyMixedPattern(mixRu2);

        // ===== 5) Специальные кейсы "я и двое детей" / "me and my two kids" / "yo y mis dos hijos" =====
        void TryApplyAdultPlusKids1(Match m, string langTag)
        {
            if (!m.Success) return;
            var gK = m.Groups["k"];
            if (!gK.Success) return;

            if (!TryParsePartyNumberToken(gK.Value, out var kids)) return;
            var total = 1 + kids; // 1 взрослый (я) + дети
            if (total < 1 || total > 10) return;

            size = Math.Max(size ?? 0, total);
            reasons.Add($"adult+kids(implicit-1,{langTag})=1+{kids}->{total}");
        }

        // EN: "me and my two kids", "me with 2 children"
        var meKidsEn = Regex.Match(
            t,
            @"\b(?:me|i)\s+(?:and|with)\s+(?:my\s+)?(?<k>\d{1,2}|" + EnNumWordPattern +
            @")\s+(?:kid(?:s)?|child(?:ren)?|children|sons?|daughters?)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        TryApplyAdultPlusKids1(meKidsEn, "en");

        // ES: "yo y mis dos hijos"
        var meKidsEs = Regex.Match(
            t,
            @"\byo\s+y\s+mis?\s+(?<k>\d{1,2}|" + EsNumWordPattern +
            @")\s+(?:hijos?|hijas?|ninos?|ninas?)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        TryApplyAdultPlusKids1(meKidsEs, "es");

        // RU: "я и двое детей", "я с двумя детьми"
        var meKidsRu = Regex.Match(
            t,
            @"\bя\s+(?:и|c)\s+(?<k>\d{1,2}|" + RuNumWordPattern +
            @")\s+дет(?:ей|и|ям|ят|ок)?\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        TryApplyAdultPlusKids1(meKidsRu, "ru");

        // Если есть mixedTotal, обновляем size
        if (mixedTotal is int mt)
        {
            size = Math.Max(size ?? 0, mt);
            // reasons уже содержит adults+kids=...
        }

        // ===== 6) Маркеры "for/для/para" (маркеры группы, размер не меняют) =====
        if (Regex.IsMatch(t, @"\b(for|для|para)\b", RegexOptions.CultureInvariant))
            reasons.Add("prep=for/для/para");

        if (ContainsWordFast(t, "people") || t.Contains("людей") || t.Contains("человек") || t.Contains("personas"))
            reasons.Add("people");

        // ===== 7) Kids / Adults / Mixed =====
        bool kidsMentioned = KidsGroupTokens.Any(k => ContainsWordFast(t, k));
        bool adultMentioned = AdultTokens.Any(k => ContainsWordFast(t, k)) || t.Contains("взросл");

        // явные смешанные фразы «мы с женой и сыном / my wife and son / mi esposa e hijo»
        bool explicitMixed =
            Regex.IsMatch(t, @"\b(wife|husband|girlfriend|boyfriend)\b.*\b(son|daughter|kid|child|children)\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) ||
            Regex.IsMatch(t, @"\b(esposa|esposo|novia|novio)\b.*\b(hijo|hija|ninos?|ninas?)\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) ||
            Regex.IsMatch(t, @"\b(жена|муж|девушка|парень)\b.*\b(сын|дочь|дет(?:и|ей|ям))\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (kidsMentioned && adultMentioned)
        {
            kind = "Mixed";
        }
        else if (explicitMixed)
        {
            kind = "Mixed";
            reasons.Add("explicit-mixed");
        }
        else if (kidsMentioned)
        {
            kind = "Kids";
        }
        else if (adultMentioned)
        {
            kind = "Adults";
        }

        return (size, kind, string.Join(",", reasons));
    }


    /// <summary>
    /// If the party is marked as "Kids", this method tries to prefer kid-friendly
    /// haircut services where it makes sense. It replaces generic/adult haircuts
    /// (e.g. "Haircut", "Men haircut", "Women haircut") with "Kids haircut".
    /// If partyKind is not "Kids", the original list is returned unchanged.
    /// </summary>
    private static List<string> PreferKidsForGroupIfNeeded(List<string> services, string partyKind)
    {
        // Gracefully handle null
        if (services == null)
            return new List<string>();

        // If this is not a kids-only party, do not modify services
        if (!string.Equals(partyKind, "Kids", StringComparison.OrdinalIgnoreCase))
            return services;

        // We clone to be safe, but you could also mutate in-place if you want
        var list = services.ToList();

        // Canonical adult haircut services that can be safely mapped to a kids haircut
        var adultHaircutCanonicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Haircut",
            "Men haircut",
            "Women haircut"
        };

        const string kidsCanonical = "Kids haircut";

        for (int i = 0; i < list.Count; i++)
        {
            var svc = list[i];
            if (string.IsNullOrWhiteSpace(svc))
                continue;

            // If it's already a kids haircut, leave it as-is
            if (svc.Equals(kidsCanonical, StringComparison.OrdinalIgnoreCase))
                continue;

            // 1) If the value is already a canonical service name
            if (adultHaircutCanonicals.Contains(svc))
            {
                list[i] = kidsCanonical;
                continue;
            }

            // 2) If it's a synonym, try to normalize via ServiceSynonyms → canonical
            if (ServiceSynonyms.TryGetValue(svc, out var canonical))
            {
                // If the synonym resolves directly to "Kids haircut" – normalize to canonical
                if (canonical.Equals(kidsCanonical, StringComparison.OrdinalIgnoreCase))
                {
                    list[i] = kidsCanonical;
                    continue;
                }

                // If the synonym resolves to an adult haircut – map it to "Kids haircut"
                if (adultHaircutCanonicals.Contains(canonical))
                {
                    list[i] = kidsCanonical;
                    continue;
                }
            }
        }

        return list;
    }

    #endregion Group / Party (count + kind) --------------------------------------------------------------------------------

    #region Services (with scoring) ----------------------------------------------------------------------------------------

    /// <summary>
    /// This method normalizes the input and checks for matches against built-in synonyms and
    /// canonical service tokens. It is useful for mapping user-entered or free-form text to a predefined set of service
    /// names, such as "Haircut", "Men haircut", or "Manicure". The matching is case-insensitive and accounts for common
    /// variations in phrasing
    /// tokens.
    /// </summary>
    public static string? TryExtractService(string text)
    {
        var norm = text.Normalize();

        // =========================
        // 1) External lexicon (if provided from DB / admin UI)
        // =========================
        //if (_lexicon is not null)
        //{
        //    // 1.1 Direct alias match (e.g. custom marketing names)
        //    var fromAlias = _lexicon.ResolveAliasInUtterance(norm);
        //    if (!string.IsNullOrEmpty(fromAlias))
        //        return fromAlias;

        //    // 1.2 Haircut + gender hints + kids hints from lexicon tokens
        //    var hc = _lexicon.HaircutTokens();
        //    var men = _lexicon.MenHintTokens();
        //    var wom = _lexicon.WomenHintTokens();
        //    var kids = CanonicalToTokens["Kids haircut"];

        //    bool hasHaircut = hc.Any(tok => ContainsWordFast(norm, tok));
        //    bool kidsHint = kids.Any(tok => ContainsWordFast(norm, tok));
        //    bool menHint = men.Any(tok => ContainsWordFast(norm, tok));
        //    bool womenHint = wom.Any(tok => ContainsWordFast(norm, tok));

        //    // “haircut” + kids hints → Kids haircut
        //    if (hasHaircut && kidsHint)
        //        return "Kids haircut";

        //    // “haircut” + (men or women) hints → choose closest / unique
        //    if (hasHaircut && (menHint || womenHint))
        //    {
        //        var hcIndex = IndexOfAnyWord(norm, hc);
        //        var menIndex = IndexOfAnyWord(norm, men);
        //        var womIndex = IndexOfAnyWord(norm, wom);

        //        // Only men hints → Men haircut
        //        if (menHint && womIndex < 0)
        //            return "Men haircut";

        //        // Only women hints → Women haircut
        //        if (womenHint && menIndex < 0)
        //            return "Women haircut";

        //        // Both present → pick the one closer to “haircut”
        //        if (hcIndex >= 0 && menIndex >= 0 && womIndex >= 0)
        //            return Math.Abs(menIndex - hcIndex) <= Math.Abs(womIndex - hcIndex)
        //                ? "Men haircut"
        //                : "Women haircut";
        //    }

        //    // Just generic haircut, no gender or kids hints
        //    if (hasHaircut)
        //        return "Haircut";

        //    // 1.3 Non-haircut codes (e.g. Color, Manicure, etc.)
        //    foreach (var code in _lexicon.AllServiceCodes()
        //                                .Where(c => c is not "Haircut"
        //                                                and not "Men haircut"
        //                                                and not "Women haircut"))
        //    {
        //        // We assume lexicon codes are canonical names,
        //        // so we just check if they appear as words in the utterance.
        //        if (ContainsWordFast(norm, code.ToLowerInvariant()))
        //            return code;
        //    }
        //}

        // 2) Built-in normalized synonyms (SynonymsNormalize)

        // Check if text contains any known synonym
        foreach (var kv in SynonymsNormalize)
        {
            if ((ContainsWordFast(norm, kv.Key)))
            {
                return kv.Value;
            }
        }

        // 3) Canonical tokens (fallback rules if no other matches found)

        // Check if text contains any canonical tokens
        bool hasHC = CanonicalToTokens["Haircut"].Any(tok => ContainsWordFast(norm, tok));
        bool kidsH = CanonicalToTokens["Kids haircut"].Any(tok => ContainsWordFast(norm, tok));
        bool menH = CanonicalToTokens["Men haircut"].Any(tok => ContainsWordFast(norm, tok));
        bool womenH = CanonicalToTokens["Women haircut"].Any(tok => ContainsWordFast(norm, tok));

        // 3.1 Haircut + kids tokens → Kids haircut
        if (hasHC && kidsH)
            return "Kids haircut";

        // 3.2 Haircut + (men/women) tokens → choose by proximity
        if (hasHC && (menH || womenH))
        {
            var hcIndex = IndexOfAnyWord(norm, CanonicalToTokens["Haircut"]);
            var menIndex = IndexOfAnyWord(norm, CanonicalToTokens["Men haircut"]);
            var womIndex = IndexOfAnyWord(norm, CanonicalToTokens["Women haircut"]);

            if (menH && womIndex < 0)
                return "Men haircut";

            if (womenH && menIndex < 0)
                return "Women haircut";// 3.2 Haircut + (men/women) tokens → choose by proximity

            if (hcIndex >= 0 && menIndex >= 0 && womIndex >= 0)
                return Math.Abs(menIndex - hcIndex) <= Math.Abs(womIndex - hcIndex)
                    ? "Men haircut"
                    : "Women haircut";
        }

        // 3.3 Pure generic haircut
        if (hasHC)
            return "Haircut";

        // 3.4 Any other canonical service by tokens 
        foreach (var kv in CanonicalToTokens)
        {
            // Skip haircuts here — already handled above
            if (kv.Key is "Men haircut" or "Women haircut" or "Haircut" or "Kids haircut")
                continue;

            if (kv.Value.Any(tok => ContainsWordFast(norm, tok)))
                return kv.Key;
        }

        // Nothing matched
        return null;
    }

    /// <summary>
    /// Tries to extract *all* service codes mentioned in the utterance.
    /// For example:
    ///   "women haircut and haircut for my son"
    /// → ["Women haircut", "Kids haircut"] (assuming proper tokens).
    ///
    /// This version:
    ///  - Uses built-in normalized synonyms (SynonymsNormalize).
    ///  - Uses canonical tokens (CanonicalToTokens) for Haircut-family and other services.
    ///  - Does NOT depend on external lexicon.
    ///  - Returns a unique set of canonical service names.
    /// </summary>
    public static List<string> TryExtractServices(string text)
    {
        // 0) Fast guard: empty / null → no services
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Normalized text is our base for all lookups
        var norm = Normalize(text);

        // We collect services in a HashSet so we don't get duplicates
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // =========================
        // 1) Built-in normalized synonyms (SynonymsNormalize)
        // =========================
        //
        // Similar to TryExtractService:
        // if the utterance contains any known synonym (already normalized key),
        // we add the corresponding canonical service code.
        //
        // Examples:
        //   "womens cut"  -> "Women haircut"
        //   "kids cut"    -> "Kids haircut"
        //   "balayage"    -> "Balayage"
        //   "mani"        -> "Manicure"
        //
        // Here we do NOT return immediately, because we want to collect
        // multiple services from the same utterance.
        foreach (var kv in SynonymsNormalize)
        {
            if (ContainsWordFast(norm, kv.Key))
                found.Add(kv.Value);
        }

        // =========================
        // 2) Haircut family (Haircut / Men / Women / Kids) via canonical tokens
        // =========================
        //
        // We derive more structured information based on canonical token lists.
        // This helps when synonyms didn't fire or the phrase is more generic
        // (e.g. "haircut for my son" without any direct synonym).
        //
        var hcTokens = CanonicalToTokens["Haircut"];
        var kidsTokens = CanonicalToTokens["Kids haircut"];
        var menTokens = CanonicalToTokens["Men haircut"];
        var womenTokens = CanonicalToTokens["Women haircut"];

        // Check if the utterance contains any tokens from these lists
        bool hasHaircut = hcTokens.Any(tok => ContainsWordFast(norm, tok));
        bool kidsHint = kidsTokens.Any(tok => ContainsWordFast(norm, tok));
        bool menHint = menTokens.Any(tok => ContainsWordFast(norm, tok));
        bool womenHint = womenTokens.Any(tok => ContainsWordFast(norm, tok));

        // 2.1 Haircut + kids tokens → Kids haircut
        if (hasHaircut && kidsHint)
            found.Add("Kids haircut");

        // 2.2 Haircut + men tokens → Men haircut
        if (hasHaircut && menHint)
            found.Add("Men haircut");

        // 2.3 Haircut + women tokens → Women haircut
        if (hasHaircut && womenHint)
            found.Add("Women haircut");

        // 2.4 Generic haircut (no men/women/kids hints)
        if (hasHaircut && !menHint && !womenHint && !kidsHint)
            found.Add("Haircut");

        // =========================
        // 3) All other canonical services (Color, Balayage, Manicure, etc.)
        // =========================
        //
        // Here we scan all canonical services and check if any of their tokens
        // appear as whole words in the normalized utterance.
        //
        foreach (var kv in CanonicalToTokens)
        {
            var canonical = kv.Key;

            // Skip the haircut family here — already handled above
            if (canonical is "Men haircut" or "Women haircut" or "Haircut" or "Kids haircut")
                continue;

            var tokens = kv.Value;

            // If any token for this canonical service appears as a whole word,
            // we add that service to the result.
            if (tokens.Any(tok => ContainsWordFast(norm, tok)))
                found.Add(canonical);
        }

        // Convert HashSet to List before returning
        return found.ToList();
    }

    /// <summary>
    /// Extracts all services from the utterance, computes a heuristic confidence score,
    /// and returns a human-readable reason string for logging/debugging.
    /// 
    /// Score is based on:
    ///   - Whether any services were found at all
    ///   - Presence of canonical haircut tokens (Haircut / Men / Women / Kids)
    ///   - Presence of normalized synonyms (SynonymsNormalize)
    ///   - Utterance length (short & clear vs. long & noisy)
    ///   - Party info (used to adjust services list, not the score directly here)
    /// </summary>
    public static (List<string> Services, double Score, string Reason) ExtractServicesWithScore(string text)
    {
        // Normalized text is our base for all lookups (tokens, synonyms, etc.)
        var norm = Normalize(text);

        // 1) Core rule-based extraction: all services from this utterance
        var services = TryExtractServices(text);

        // =========================
        // 2) Base score: do we have any services at all?
        // =========================
        //
        // If we found at least one service, we start at 0.60.
        // If we found nothing, we start at 0.0 → likely need clarification or LLM fallback.
        //
        double score = services.Count > 0 ? 0.6 : 0.0;

        // =========================
        // 3) Canonical haircut tokens (Haircut / Men / Women / Kids)
        // =========================
        //
        // These are strong signals that the user really talked about a haircut.
        //
        bool hasHaircutTok = CanonicalToTokens["Haircut"]
            .Any(tok => ContainsWordFast(norm, tok));

        bool menHint = CanonicalToTokens["Men haircut"]
            .Any(tok => ContainsWordFast(norm, tok));

        bool womenHint = CanonicalToTokens["Women haircut"]
            .Any(tok => ContainsWordFast(norm, tok));

        bool kidsHint = CanonicalToTokens["Kids haircut"]
            .Any(tok => ContainsWordFast(norm, tok));

        // Haircut-token present → increase confidence
        if (hasHaircutTok)
            score += 0.15;

        // Any gender/kids hints → slightly more confidence
        if (menHint || womenHint || kidsHint)
            score += 0.10;

        // =========================
        // 4) Synonym hit (SynonymsNormalize) → +0.05
        // =========================
        //
        // If a service was matched via our curated synonyms dictionary,
        // it’s a strong positive signal (admin/owner defined these),
        // so we reward it a bit.
        //
        bool synonymHit = false;
        foreach (var kv in SynonymsNormalize)
        {
            if (ContainsWordFast(norm, kv.Key))
            {
                synonymHit = true;
                break;
            }
        }

        if (synonymHit && services.Count > 0)
        {
            // Small bonus for “direct synonym match”
            score += 0.05;
        }

        // =========================
        // 5) Utterance length heuristics (adaptive scoring)
        // =========================
        //
        // Idea:
        //   - Very short phrases with clear services are usually high-signal (“just a women’s cut”).
        //   - Very long phrases with only one service can be more noisy and ambiguous
        //     (“so I was thinking maybe next week, maybe a haircut or maybe something else...”).
        //
        var tokens = norm
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); // split on whitespace
        int tokenCount = tokens.Length;

        if (services.Count > 0)
        {
            // 5.1 Short & focused utterance (e.g. <= 4 words) → more confidence
            //     Examples: "women haircut", "kids haircut", "just balayage"
            if (tokenCount <= 4)
            {
                score += 0.05;
            }
            // 5.2 Long utterance with only one service → slightly less confidence
            //     Example: "so I wanted to ask if you work on Sundays and maybe do a quick haircut for me..."
            else if (tokenCount >= 20 && services.Count == 1)
            {
                score -= 0.05;
            }
        }

        // Make sure score doesn’t go below 0 after deductions
        if (score < 0.0)
            score = 0.0;

        // =========================
        // 6) Party info and service adjustment for groups
        // =========================
        //
        // We don’t directly change the score here, but we DO adjust
        // the services list based on party kind (KidsOnly, Mixed, etc.).
        //
        var party = TryExtractParty(text);
        services = PreferKidsForGroupIfNeeded(services, party.kind);

        // =========================
        // 7) Clamp score to max 0.95 and build reason string
        // =========================
        //
        // We keep a small gap to 1.0 so that other layers (e.g. LLM) can still
        // reason “there is some residual uncertainty”.
        //
        if (score > 0.95)
            score = 0.95;

        var reasonStr =
            $"rules: {string.Join("|", services)}; " +
            $"hc={hasHaircutTok}; men={menHint}; women={womenHint}; kids={kidsHint}; " +
            $"synonym={synonymHit}; tokens={tokenCount}; " +
            $"party=({party.size},{party.kind})";

        return (services, score, reasonStr);
    }

    #endregion Services (with scoring) -------------------------------------------------------------------------------------

    #region Master ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Tries to extract master name from the text if mentioned by caller (e.g. "with John", "by Anna", "I want to book with Mike").
    /// </summary>
    public static string? TryExtractMasterByName(string text)
    {
        var norm = Normalize(text);

        // 1) Direct name match
        var m1 = RxWithMaster.Match(norm);

        // 2) Return canonicalized name if found
        if (m1.Success) 
            return CanonicalizeName(m1.Groups[1].Value);

        // 3) Return canonicalized name if did not find in 2
        var m2 = RxMasterAny.Match(norm);
        if(m2.Success) 
            return CanonicalizeName(m2.Groups[1].Value);

        // 4) Try master synonyms if nothing found yet
        foreach (var kv in MasterNormalize)
            if (ContainsWordFast(norm, Normalize(kv.Key)))
                return kv.Value;

        return null;
    }

    public static (bool noPref, string[] prefered) ParseMasterPreference(string text)
    {
        var norm = Normalize(text);

        // 1) If no preference mentioned
        if (RxNoMasterPreference.IsMatch(norm))
            return (true, Array.Empty<string>());

        // 2) Collect all mentioned masters
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 3) Source of masters
        // TODO (future): enable when _catalog is implemented
        // string[] masters = _catalog?.GetMasters() ?? Masters;

        string[] master = Masters;

        // 4) Direct matches: "Emily", "Nikita", "Pablo"
        foreach (var m in master)
            if(ContainsWordFast(norm, Normalize(m)))
                found.Add(m);

        // 5) Normalization dictionary: "emi" → Emily, "никитка" → Nikita
        foreach (var kv in MasterNormalize)
        {
            if (ContainsWordFast(norm, Normalize(kv.Key)))
                found.Add(kv.Value);
        }

        // 6) If nothing found, return no preference
        return (found.Count == 0, found.ToArray());
    }

    /// <summary>
    /// Filters the list of available masters to those who support all specified services.
    /// </summary>
    public static IEnumerable<string> FilterMastersBySkills (IEnumerable<string> services, IEnumerable<string>? preferredMasters = null)
    {
        // Create a set of needed services for quick lookup  
        var needed = new HashSet<string>(services, StringComparer.OrdinalIgnoreCase);

        // Determine candidate masters based on preferences or all available masters
        var candidate = (preferredMasters is not null && preferredMasters.Any())
            ? new HashSet<string>(preferredMasters, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(// _catalog?.GetMasters() ?? TODO (future): enable when _catalog is implemented
                                  Masters, StringComparer.OrdinalIgnoreCase);

        // TODO(future): enable when _catalog is implemented
        // if (_catalog is not null)
        // return candidates.Where(m => needed.All(svc => _catalog.Supports(m, svc)));

        // Filter candidates based on their skills
        return candidate.Where(m => MasterSkills.TryGetValue(m, out var skills)
               && needed.All(svc => skills.Contains(svc)));
    }

    #endregion Master ------------------------------------------------------------------------------------------------------

    #region Recognizers helpers --------------------------------------------------------------------------------------------

    /// <summary>
    /// Picks culture ("es-es" vs "en-us") based on presence of strong Spanish markers in the text.
    /// </summary>
    private static string PickCulture(string text)
    {
        // Normalize уже делает lower + убирает диакритику:
        // "Mañana a las tres" -> "manana a las tres"
        var t = Normalize(text);

        int scoreEs = 0;

        // 1) Сильные маркеры: дни недели + mañana/hoy/tarde/noche
        if (Regex.IsMatch(t, @"\b(lunes|martes|miercoles|jueves|viernes|sabado|domingo)\b",
                          RegexOptions.CultureInvariant))
            scoreEs += 2;

        if (Regex.IsMatch(t, @"\b(manana|hoy|tarde|noche)\b",
                          RegexOptions.CultureInvariant))
            scoreEs += 2;

        // 2) Типичные салонные/бронированные слова на испанском
        if (Regex.IsMatch(t,
                @"\b(cita|citas|turno|turnos|agendar|reservar|reserva|agendo|reservo|disponible|disponibilidad)\b",
                RegexOptions.CultureInvariant))
            scoreEs += 1;

        // 3) Частотные глаголы и местоимения, но слабее (балл поменьше)
        if (Regex.IsMatch(t,
                @"\b(quiero|quisiera|necesito|puedo|podria|horario|hora|horas|nosotros|nosotras|ustedes|gracias|por favor)\b",
                RegexOptions.CultureInvariant))
            scoreEs += 1;

        // Порог: 2 и выше -> считаем испанским
        if (scoreEs >= 2)
            return Culture.Spanish;   // "es-es"

        return Culture.English;       // "en-us"
    }

    /// <summary>
    /// Recognizes party size using Microsoft Recognizers Text (MSRT) library
    /// </summary>
    private static int? RecognizePartySizeWithMsrt(string text)
    {
        var culture = PickCulture(text);
        var nums = NumberRecognizer.RecognizeNumber(text, culture);
        int? best = null;

        foreach (var n in nums)
        {
            if (n.Resolution != null && n.Resolution.TryGetValue("value", out var valObj))
            {
                if (valObj is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    var cand = (int)Math.Round(d);
                    if (cand >= 1 && cand <= 10) best = Math.Max(best ?? 0, cand);
                }
            }
        }

        // simple phrasing
        if (best is null)
        {
            var t = Normalize(text);
            if (ContainsWordFast(t, "couple") || ContainsWordFast(t, "both") || ContainsWordFast(t, "pair")) best = 2;
            if (ContainsWordFast(t, "pareja")) best = 2;
        }

        return best;
    }

    #endregion Recognizers helpers --------------------------------------------------------------------------------------------

    #region Duration / YesNo / Helpers ----------------------------------------------------------------------------------------

    private static int IndexOfAnyWord(string text, IEnumerable<string> words)
    {
        int best = -1;
        foreach (var w in words)
        {
            int idx = IndexOfWord(text, w);
            if (idx >= 0 && (best < 0 || idx < best)) best = idx;
        }
        return best;
    }

    private static int IndexOfWord(string text, string word)
    {
        if (string.IsNullOrEmpty(word)) return -1;
        int idx = text.IndexOf(word, StringComparison.Ordinal);
        while (idx >= 0)
        {
            bool leftOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            int end = idx + word.Length;
            bool rightOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (leftOk && rightOk) return idx;
            idx = text.IndexOf(word, idx + 1, StringComparison.Ordinal);
        }
        return -1;
    }

    private static string TitleCase(string name)
    {
        var ti = CultureInfo.InvariantCulture.TextInfo;
        return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => ti.ToTitleCase(w.ToLowerInvariant())));
    }

    private static string CanonicalizeName(string found)
    {
        // For DB-based catalog
        //if (_catalog is not null) return _catalog.Canonicalize(found);
        var n = Normalize(found);

        if (MasterNormalize.TryGetValue(n, out var canonFromDict)) return canonFromDict;

        foreach (var m in Masters) if (Normalize(m).Equals(n, StringComparison.OrdinalIgnoreCase)) return m;
        foreach (var m in Masters) if (LooseEq(m, found)) return m;
        return TitleCase(found);
    }

    private static string NormalizeLite(string s) => Regex.Replace(s, @"\s+", " ").Trim();

    private static bool LooseEq(string a, string b) => NormalizeLite(a).Equals(NormalizeLite(b), StringComparison.OrdinalIgnoreCase);

    #endregion Duration / YesNo / Helpers -------------------------------------------------------------------------------------
}