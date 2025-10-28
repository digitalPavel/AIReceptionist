using Demo1.Models;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Demo1.Services.Brain;

/// <summary>
/// NLU layer for the better intantion understanding and perfomance.
/// Also to avoid extra LLM usage to save cost.
/// </summary>
public static class IntentRules
{
    // Defined words/phrases for intent recognition. Use * as a wildcard for stemming.

    // BOOK — create a new appointment
    private static readonly string[] BookWords =
    {
    // EN — booking intent
    "book","booking","make an appointment","set up an appointment","book an appointment",
    "i’d like to book","i would like to book","can i book","schedule me","schedule appointment",
    "same day appointment","do you have anything today","next available","earliest available",
    "available today","available now","time slot","slot","walk in","walk-in",
    "consultation","free consult","new client appointment","first time appointment", "appointmen*", 

    // EN — service-led booking triggers
    "book a haircut","book haircut","book trim","book color","book highlights",
    "book balayage","book blowout","book keratin","book extensions",

    // RU — запись
    "запис*","бронь","забронировать","назначить приём","назначить прием","встать на запись",
    "хочу записаться","можно записаться","записаться сегодня","запись сегодня",
    "свободные слоты","ближайшее время","самое раннее","консультация",
    "первичная консультация","впервые записаться","записаться на стрижку","записаться на окрашивание",

    // ES — cita
    "cita","reserv*","agend*","turno","apart* cita",
    "sacar cita","pedir cita","hacer una cita","agendar una cita",
    "quiero una cita","tienen disponibilidad","hay disponibilidad",
    "hoy mismo","para hoy","primera hora",
    "cita primera vez","cita para corte","cita para color","cita para balayage","cita para keratina"
    };

    // RESCHEDULE — change existing appointment
    private static readonly string[] RescheduleWords =
    {
    // EN
    "reschedule", "reschedul*","rebook","change time","change my appointment","move appointment","move my appointment",
    "reschedule my appointment","different time","another time","another day",
    "switch time","push back","bring forward","earlier time","later time",
    "move to tomorrow","move to next week","any time tomorrow","same time tomorrow",

    // RU
    "перенес*","перезапис*","сдвин*","перекин*",
    "на другое время","на другой день","пораньше","попозже",
    "перенести на завтра","перенести на следующую неделю","в это же время завтра",
    "заменить время","изменить время","поменять время","перенести на","перезаписать меня",

    // ES
    "reprogram*","cambiar hora","cambiar mi cita","mover cita","otra hora","otro día",
    "posponer","adelantar","pasar para mañana","la próxima semana","reprogramar cita"
    };

    // CANCEL — cancel existing appointment
    private static readonly string[] CancelWords =
    {
    // ==============================
    // EN — cancel existing appointment
    // ==============================
    "cancel appointment","cancel my appointment","cancel the appointment",
    "cancel my booking","cancel the booking","cancel my appt","cancel appt",
    "i need to cancel","i have to cancel","please cancel",
    "can you cancel","could you cancel",
    "call off appointment","drop the appointment","remove my booking",
    "i want to cancel","i do not need the appointment anymore",
    "i cannot come","i can't come","cant come","cannot make it",
    "can’t make it","cant make it","won’t make it","wont make it",
    "not coming","not going to make it",
    "running late cancel","sick cancel","i am sick","sick today",
    "i have an emergency","family emergency",
    "traffic delay cannot make it",
    "no longer need","do not need anymore",

    // ==============================
    // RU — отменить запись
    // ==============================
    "отменить запись","отмена записи","удалите запись",
    "убрать запись","снять запись","отказаться от записи",
    "хочу отменить","передумал","неактуально","больше не надо",
    "не смогу прийти","не смогу","не получится прийти","не получится",
    "заболел","приболел","болею","плохо себя чувствую",
    "не приду","опаздываю не приду", "отмените мою запись", "отмен*", "визит", "запис*",
    "отмен*",
    "семейные обстоятельства","форс мажор",

    // ==============================
    // ES — cancelar cita
    // ==============================
    "cancelar la cita","cancelar mi cita","anular cita","anular la cita",
    "por favor cancela","por favor cancelar",
    "quiero cancelar","tengo que cancelar","necesito cancelar",
    "no puedo ir","no voy a ir","ya no la necesito","no la necesito",
    "no podré asistir","no puedo asistir",
    "estoy enfermo","estoy enferma","estoy mal","enfermo hoy",
    "emergencia familiar","no llego a tiempo"
};

    // HANDOFF — request for human / language escalation
    private static readonly string[] HandoffWords =
    {
    // EN — direct human requests
    "operator","human","representative","agent","live agent","real person","receptionist","front desk","staff","manager",
    "speak to a human","talk to a human","talk to someone","speak to someone",
    "speak to representative","talk to representative",
    "connect me","transfer me","connect to human","transfer to human",
    "can i talk to someone","i need a person","i want to talk to a person","human please","someone else please",

    // EN — frustration signals that imply escalation
    "this is not helping","i do not understand","not working","stop the robot","i need assistance","let me talk to someone",

    // EN — language routing
    "english please","spanish please","russian please","spanish speaker","russian speaker",
    "do you speak russian","do you speak spanish","i need spanish","i need russian","help in spanish","help in russian",

    // RU — человек нужен
    "оператор","консультант","администратор","менеджер","живой человек","настоящий человек",
    "соедините","соедините с оператором","переключите","переведите на оператора",
    "хочу поговорить с человеком","можно с человеком",
    "не понимаю","это не помогает","нужна помощь","нужен менеджер",
    "можно по-русски","говорите по-русски","говорит по-русски","помощь по-русски","русский язык",

    // ES — humano / idioma
    "humano","agente","representante","operador","persona real","recepcionista","encargado",
    "hablar con alguien","hablar con un representante","hablar con una persona",
    "conéctame","transferirme","conectar con humano","esto no ayuda","no entiendo","necesito ayuda",
    "en español por favor","habla español","hablan español",
    "puedo hablar español","necesito español","ayuda en español",
    "en ruso por favor","habla ruso","hablan ruso"
    };

    // FAQ — pricing, hours, policies, location, payments, service info
    private static readonly string[] FaqWords =
    {
    // EN — pricing
    "price","prices","pricing","cost","how much","fee","fees","estimate","quote",
    "price list","service menu","menu","rates","discount","coupon","specials",
    "starting at","from price","price range","consultation fee",

    // EN — services asked for price
    "haircut","trim","fade","layers","bangs","fringe",
    "color","dye","bleach","highlights","lowlights","balayage","toner","ombre",
    "blowout","style","keratin","extensions","braids",

    // EN — hours & availability
    "hours","business hours","opening hours","store hours","salon hours",
    "what time do you open","when do you open","what time do you close","when do you close",
    "open today","open now","closed now",
    "walk-ins welcome","do you take walk-ins",
    "availability","next availability","next opening",
    "weekend hours","weekday hours","holiday hours","are you open on","are you open today",
    "when you opened","when you clsoed", // recall-only misspellings

    // EN — location & contact
    "address","location","directions","how do i get there","near me","parking","where to park",
    "phone number","contact","website","instagram","facebook",

    // EN — policies & payments
    "new client policy","cancellation policy","late policy","no show fee",
    "deposit","prepayment","refund policy","no refunds",
    "credit card","debit card","cash only","card only","tap to pay","apple pay","google pay","zelle","venmo","cash app","gratuity included",

    // RU — цены / меню / услуги
    "цена","цены","стоимость","сколько стоит","сколько будет","прайс","меню услуг","прейскурант",
    "консультация","бесплатная консультация",
    "стрижка","окрашивание","балаяж","мелирование","укладка","кератин","наращивание","пряди",

    // RU — часы / доступность / адрес
    "часы работы","график","режим работы","расписание",
    "во сколько открываетесь","во сколько закрываетесь","вы сегодня открыты","сейчас открыты",
    "без записи","есть запись сегодня",
    "адрес","локация","как добраться","рядом","парковка","телефон","сайт","инстаграм","фейсбук",

    // RU — политики / оплата
    "залог","предоплата","политика отмен","штраф за неявку","возврат средств","без возвратов",
    "оплата","картой","наличными","apple pay","google pay","чаевые включены",

    // ES — precios / servicios
    "precio","precios","cuánto cuesta","lista de precios","menú de servicios",
    "corte","color","balayage","queratina","extensiones",
    "consulta","consulta gratis",

    // ES — horario / acceso
    "horario","a qué hora abren","a qué hora cierran","están abiertos hoy",
    "abierto ahora","cerrado ahora",
    "sin cita","cita el mismo día","walk in",
    "dirección","ubicación","cómo llegar","cerca de mí","estacionamiento","validación de estacionamiento",
    "teléfono","sitio web","redes sociales","instagram","facebook",

    // ES — políticas / pago
    "política de cancelación","cargo por tardanza","cargo por no show","política nuevos clientes",
    "depósito","anticipo","reembolso","sin reembolsos",
    "tarjeta","efectivo","solo efectivo","apple pay","google pay","propina incluida"
};

    // Neutral tokens that do not contribute to intent scoring
    private static readonly HashSet<string> NeutralTokens = new(StringComparer.Ordinal)
    {
        // ==============================
        // EN — polite / connector / filler
        // ==============================
        "hi","hello","hey","thanks","thank you","ok","okay","please",
        "yes","no","sure","maybe","fine","cool","alright","right","well",

        // Generic conversational helpers
        "like","just","also","really","actually","so","then",

        // Soft temporal context (not directive)
        "today","tomorrow","morning","afternoon","evening","tonight","soon","later",
        "this","next","day","time",

        // ==============================
        // RU — нейтральные
        // ==============================
        "привет","здравствуйте","ок","хорошо","пожалуйста","спасибо","ага",
        "да","нет","можно","ладно",

        // Время
        "сегодня","завтра","утром","днем","вечером","позже","скоро","сейчас",

        // ==============================
        // ES — нейтральные
        // ==============================
        "hola","buenas","gracias","ok","vale","claro","por favor",
        "sí","si","no","tal vez","quizas","quizás",

        // Tiempo
        "hoy","mañana","tarde","noche","ahora","pronto","después"
    };


    // Mapping from Intent to associated keywords/phrases
    private static readonly Dictionary<Intent, string[]> Vocab = new()
    {
        { Intent.Book, BookWords },
        { Intent.Cancel, CancelWords},
        { Intent.Reschedule, RescheduleWords },
        { Intent.Handoff, HandoffWords},
        { Intent.Faq, FaqWords },
    };

    // Score weights for each intent when multiple intents are detected
    private static readonly Dictionary<Intent, int> Priorities = new()
    {
        { Intent.Book, 50},
        { Intent.Cancel, 60},
        { Intent.Reschedule, 55},
        { Intent.Handoff, 100},
        { Intent.Faq, 20},
        { Intent.Unknown, 0},
    };

    // Safe preprocess for hyphens/underscores/slashes/dots -> spaces (before tokenization).
    private static readonly Regex SplitterChars =
        new(@"[-_/\.]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Tokenizer(a tokenization step) that breaks a sentence into separate tokens (words and numbers)
    private static readonly Regex Tokenizer =
        new(@"\p{L}(?:[\p{L}\p{M}]*)|\p{N}+", 
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Record with normalized tokens, prefixes, phrases, and priority
    private sealed record LexiconRuntime(
        HashSet<string> Words,   // Exact single-word tokens (normalized)
        string[] PrefixWords,    // Word stems without '*' (normalized)
        string[] Phrases,        // Multi-word phrases with boundary checks (normalized)
        int Priority             // Priority score weight for tie-breaking between intents
    );

    // Precompiled lexicons dictionary with normalized tokens, prefixes, phrases, and priority
    // for each intent(initialized once at startup and reused for fast classification)
    private static readonly Dictionary<Intent, LexiconRuntime> _lex;

    // Static constructor — builds runtime lexicon once at startup
    static IntentRules()
    {
        // Create main dictionary to store lexicons for each intent
        _lex = new Dictionary<Intent, LexiconRuntime>(Vocab.Count);

        // Process each intent (Book, Cancel, etc.) to build its runtime structure
        foreach (var (intent, rawList) in Vocab)
        {
            // Prepare collections for words, prefixes, and phrases for current intent
            var words = new HashSet<string>(rawList.Length, StringComparer.Ordinal);
            var prefixes = new List<string>(Math.Min(8, rawList.Length));
            var phrases = new List<string>(rawList.Length);

            // Loop through each raw keyword/phrase in the vocab
            // Iterate through each raw vocabulary entry for this intent
            foreach (var raw in rawList)
            {
                // Ignore empty or whitespace-only entries
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                // Determine if this vocabulary item uses a wildcard prefix (*) BEFORE Normalize
                // Because Normalize removes '*' automatically as a symbol
                bool isPrefix = raw.EndsWith("*", StringComparison.Ordinal);

                // Remove '*' only for prefix stems BEFORE normalization
                string core = isPrefix ? raw[..^1] : raw;

                // Normalize: lowercase + remove diacritics + punctuation cleanup
                var needle = Normalize(core);

                // Multi-word phrase → store as a phrase for boundary-checked scoring
                if (needle.Contains(' '))
                {
                    phrases.Add(needle.Trim());
                }
                // Prefix word → store its normalized stem as a prefix trigger
                else if (isPrefix)
                {
                    // Minimal length check helps avoid false positives (e.g. "re*")
                    if (!string.IsNullOrWhiteSpace(needle) && needle.Length >= 3)
                        prefixes.Add(needle);
                }
                // Otherwise treat as a single-word exact match term
                else
                {
                    words.Add(needle);
                }
            }

            // Remove duplicates and empty phrases
            var phraseArray = phrases
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            // Get priority for this intent (fallback = 0)
            Priorities.TryGetValue(intent, out var prio);

            // Create and store prebuilt runtime lexicon for current intent
            _lex[intent] = new LexiconRuntime(
                Words: words,
                PrefixWords: prefixes.ToArray(),
                Phrases: phraseArray,
                Priority: prio
            );
        }
    }

    //=====================Public API(Enterence to the NLU)===============================
    //=====================Here is the core of the NLU logic===============================

    /// <summary>
    /// Return just the classified intent without confidence details
    /// </summary>
    public static Intent Classify(string text) => ClassifyWithConfidence(text).Intent;

    /// <summary>
    /// Classify intent with confidence scores and detailed reasoning
    /// </summary>
    public static IntentResult ClassifyWithConfidence(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return new IntentResult(Intent.Unknown, 0.0, 0, 0, "empty input");

        // Normalize input text: replace split chars and remove diacritics
        var norm = Normalize(text);

        // Tokenize normalized text into a set of unique tokens for fast lookup
        var tokens = Tokenizer.Matches(norm)
            .Select(m => m.Value)
            .ToHashSet(StringComparer.Ordinal);

        // Track best and second-best intent matches
        (Intent intent, int score, int prio) best = (Intent.Unknown, 0, int.MinValue);
        (Intent intent, int score, int prio) second = (Intent.Unknown, 0, int.MinValue);

        // The list contains an explanation why an intent was chosen
        var reasons = new List<string>(8);

        // Go through each intent lexicon
        foreach (var(intent, lx) in _lex)
        {
            // Compute score for this intent
            var (score, hits, details) = ScoreIntent(norm, tokens, lx);

            // If there are matches, add to reason details
            if (hits > 0 )
                reasons.Add($"{intent}:{details}");

            // Update first place (best) if the current intent scores higher,
            // or if scores are tied but this intent has a higher priority.
            // The previous best gets pushed down to second place.
            if (score > best.score ||
                (score == best.score && lx.Priority > best.prio))
            {
                second = best;
                best = (intent, score, lx.Priority);
            }
            // Otherwise, update second place if the current intent is better
            // than the existing second best (same tie-break rule by priority).
            else if (score > second.score ||
                     (score == second.score && lx.Priority > second.prio))
            {
                second = (intent, score, lx.Priority);
            }
        }

        // 1No matches at all → Unknown
        if (best.score <= 0)
            return new IntentResult(Intent.Unknown, 0.0, 0, 0, "no matches");

        // Compute relative confidence between best & second-best intents
        // (prevents score inflation when vocab grows)
        var conf = best.score / (double)(best.score + second.score + 1);
        var reasonText = string.Join("; ", reasons);

        // Not enough evidence → Unknown
        if (best.score < MIN_SCORE)
            return new IntentResult(Intent.Unknown, conf, best.score, second.score,
                $"weak signal: score={best.score}; {reasonText}");

        // Too close to the next intent → ambiguous → Unknown
        if (best.score - second.score < MIN_GAP)
            return new IntentResult(Intent.Unknown, conf, best.score, second.score,
                $"narrow margin: best-second={best.score - second.score}; {reasonText}");

        // Low confidence → still Unknown (reject guess)
        if (conf < REJECT_THRESHOLD)
            return new IntentResult(Intent.Unknown, conf, best.score, second.score,
                $"low confidence: conf={conf:0.00}; {reasonText}");

        // Medium confidence → suggest intent, but caller may clarify
        if (conf < CONFIRM_THRESHOLD)
            return new IntentResult(best.intent, conf, best.score, second.score,
                $"tentative: conf={conf:0.00}; {reasonText}");

        // High confidence → accept intent fully 
        return new IntentResult(best.intent, conf, best.score, second.score, reasonText);
    }


    //=====================Internal helpers==================================================

    //Weights(poins) for different match types.
    //This defines how much “value” or “importance”
    //each kind of match adds to the total score.
    //I can tune these weights to adjust the sensitivity!
    private const int WORD_WEIGHT = 1;
    private const int PREFIX_WEIGHT = 1;
    private const int PHRASE_WEIGHT = 4;

    // Decision thresholds for intent validation:
    // - REJECT_THRESHOLD: below this confidence → intent is Unknown (reject guess)
    // - CONFIRM_THRESHOLD: above this confidence → accept intent without clarification
    // - MIN_SCORE: minimum evidence required before considering an intent valid
    // - MIN_GAP: best score must exceed second best by at least this much (avoid ties)
    private const double REJECT_THRESHOLD = 0.50;
    private const double CONFIRM_THRESHOLD = 0.65;
    private const int MIN_SCORE = 2;
    private const int MIN_GAP = 1;

    /// <summary>
    /// Calculates the intent score based on:
    ///  1) Exact word matches
    ///  2) Prefix matches
    ///  3) Phrase matches
    ///
    /// IMPORTANT:
    /// - Phrase hits come first and block their tokens
    ///   from being counted again as words/prefixes.
    /// - Neutral tokens never score (shared, non-informative words).
    ///
    /// Returns:
    ///   score   = total weighted score
    ///   hits    = number of matched elements
    ///   details = summary string for logs
    /// </summary>
    private static (int score, int hits, string details) ScoreIntent(
        string normalizedText,
        HashSet<string> tokenSet,
        LexiconRuntime lx)
    {
        int score = 0;
        int hits = 0;
        var detailList = new List<string>(4);

        // 1) Phrase matches (highest weight)
        int phraseHits = 0;
        var matchedPhrases = new List<string>();

        foreach (var p in lx.Phrases)
            if (ContainsPhraseWithBoundaries(normalizedText, p))
                matchedPhrases.Add(p);

        if (matchedPhrases.Count > 0)
        {
            phraseHits = matchedPhrases.Count;
            score += phraseHits * PHRASE_WEIGHT;
            hits += phraseHits;
            detailList.Add($"phrases:{phraseHits}");
        }

        // Block tokens that belong to matched phrases (avoid double scoring)
        var blockedTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ph in matchedPhrases)
            foreach (Match m in Tokenizer.Matches(ph))
                blockedTokens.Add(m.Value);

        // 2) Exact word matches (skip blocked + neutral)
        int wordHits = 0;
        foreach (var w in lx.Words)
        {
            if (blockedTokens.Contains(w)) continue;
            if (NeutralTokens.Contains(w)) continue;

            if (tokenSet.Contains(w))
                wordHits++;
        }

        if (wordHits > 0)
        {
            score += wordHits * WORD_WEIGHT;
            hits += wordHits;
            detailList.Add($"words:{wordHits}");
        }

        // 3) Prefix matches (e.g., cancel*, reschedul*) — skip blocked + exact words + neutral
        int prefHits = 0;
        if (lx.PrefixWords.Length > 0)
        {
            IEnumerable<string> baseTokens = tokenSet;

            if (lx.Words.Count > 0) baseTokens = baseTokens.Except(lx.Words);
            if (blockedTokens.Count > 0) baseTokens = baseTokens.Except(blockedTokens);
            if (NeutralTokens.Count > 0) baseTokens = baseTokens.Except(NeutralTokens);

            foreach (var tok in baseTokens)
            {
                foreach (var pref in lx.PrefixWords)
                {
                    if (pref.Length < 3) continue; // avoid false positives
                    if (tok.StartsWith(pref, StringComparison.Ordinal))
                    {
                        prefHits++;
                        break; // one prefix hit per token
                    }
                }
            }
        }

        if (prefHits > 0)
        {
            score += prefHits * PREFIX_WEIGHT;
            hits += prefHits;
            detailList.Add($"prefixes:{prefHits}");
        }

        return (score, hits, string.Join(", ", detailList));
    }

    /// <summary>
    /// Determines whether the specified phrase appears in the input string as a standalone word or phrase, bounded by
    /// non-word characters or string boundaries.
    /// </summary>
    private static bool ContainsPhraseWithBoundaries(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return false;

        int idx = 0;
        int nlen = needle.Length;

        // Find every occurrence of the phrase
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            // Check left and right boundaries (make sure it's not inside another word)
            bool leftOk = idx == 0 || !IsWordChar(haystack[idx - 1]);
            int right = idx + nlen;
            bool rightOk = right >= haystack.Length || !IsWordChar(haystack[right]);
            // If both sides are clean → valid phrase found
            if (leftOk && rightOk)
                return true;
            // Move forward to keep searching
            idx += 1;
        }
        // No valid phrase found
        return false;
    }

    /// <summary>
    /// Determines whether the specified character is considered part of a word for text processing purposes.
    /// </summary>
    private static bool IsWordChar(char c)
    {
        var cat = char.GetUnicodeCategory(c);
        return char.IsLetterOrDigit(c)
               || cat == UnicodeCategory.NonSpacingMark
               || cat == UnicodeCategory.SpacingCombiningMark;
    }


    /// <summary>
    /// Normalize text: lowercase + remove diacritics for clean comparison
    /// </summary>
    private static readonly Regex PunctOrSymbol =
    new(@"[\p{P}\p{S}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MultiSpace =
        new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        // 1) lower + decompose
        var lower = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);

        // 2) strip diacritics
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);

        // 3) стандартизировать тех. разделители и пунктуацию → пробел
        var splitStandardized = SplitterChars.Replace(noDiacritics, " ");
        var noPunct = PunctOrSymbol.Replace(splitStandardized, " ");

        // 4) схлопнуть пробелы и trim
        return MultiSpace.Replace(noPunct, " ").Trim();
    }

    /// <summary>
    /// Result of intent classification with confidence and scores
    /// </summary>
    public readonly record struct IntentResult(  
       Intent Intent,
       double Confidence, // 0.0–1.0 (relative)
       int Score,         // best score
       int SecondBest,    // runner-up score (for telemetry/margins)
       string Reason      // compact per-intent hits summary
    );

};