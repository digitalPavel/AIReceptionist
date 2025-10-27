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

    private static readonly string[] BookWords =
    {
        // EN — generic booking
        "book","booking","appointment","appt","schedule","set up","reserve","reservation",
        "slot","time slot","availability","available today","available now",
        "next available","earliest available","first available",
        "book an appointment","make an appointment","set up an appointment",
        "i need an appointment","i’d like to book","i would like to book","can i book","schedule me",
        "same day appointment","same-day appointment","walk in","walk-in","walk in appointment","walk-in appointment",
        "consultation","free consult","new client appointment","first time appointment",

        // RU — generic booking
        "запис*","бронь","забронировать","назначить приём","назначить прием","встать на запись",
        "хочу записаться","можно записаться","записаться сегодня","запись сегодня",
        "свободные слоты","есть свободно","ближайшее время","самое раннее","консультация",
        "первичная консультация","впервые записаться",

        // ES — generic booking
        "cita","reserv*","agend*","turno","reservación","apart* cita",
        "sacar cita","pedir cita","hacer una cita","agendar una cita",
        "quiero una cita","tienen disponibilidad","hay disponibilidad",
        "hoy mismo","para hoy","primera hora","consulta","consulta gratis","cita primera vez",
    };

    private static readonly string[] CancelWords =
    {
        // EN
        "cancel*","cancellation","call off","drop the appointment","remove my booking",
        "no longer need","have to cancel","need to cancel","please cancel",
        "can’t make it","cant make it","cannot make it","won’t make it","won't make it",
        "running late cancel","sick cancel","cancel appointment","cancel my appointment",

        // RU
        "отмен*","снять запись","убрать запись","отказаться от записи","передумал", "удалить запись",
        "не смогу прийти","не получится прийти","заболел","приболел","перестало быть актуально",

        // ES
        "cancelar","anular","anulación","quitar la cita","cancela mi cita",
        "no puedo ir","no voy a ir","ya no la necesito","por favor cancela",
    };

    private static readonly string[] RescheduleWords =
    {
        // EN
        "reschedul*","rebook","change time","change my appointment","move appointment",
        "move my appointment","different time","another time","another day",
        "switch time","push back","bring forward","earlier time","later time",
        "move to tomorrow","move to next week","any time tomorrow","same time tomorrow",

        // RU
        "перенес*","перезапис*","сдвин*","перекин*",
        "на другое время","на другой день","пораньше","попозже",
        "перенести на завтра","перенести на следующую неделю","в это же время завтра",
        "заменить время","изменить время","поменять время",

        // ES
        "reprogram*","cambiar hora","mover cita","otra hora","otro día",
        "posponer","adelantar","pasar para mañana","la próxima semana",
        "cambiar mi cita","mover mi cita",
    };

    private static readonly string[] HandoffWords =
    {
        // EN
        "operator","human","representative","agent","live agent","real person",
        "receptionist","front desk","staff","manager",
        "talk to a human","talk to someone","speak to representative",
        "transfer me","connect me","can i talk to someone",
        "english please","spanish speaker","russian speaker",

        // RU
        "оператор","человек","живой человек","администратор","менеджер",
        "соедините","переключите","переведите на оператора",
        "можно по-русски","говорит по-русски",

        // ES
        "humano","agente","operador","persona real","recepcionista","encargado",
        "transferirme","conectarme","hablar con alguien","hablar con un representante",
        "habla español","en español por favor",
    };

    private static readonly string[] FaqWords =
    {
        // EN — pricing/policies/general info
        "price","pricing","cost","how much","fee","quote","estimate","starting at",
        "menu","service menu","price list","price range",
        "deposit","booking fee","prepayment","non refundable","non-refundable",
        "cancellation policy","late policy","no show","no-show","grace period",
        "new client policy","children policy","pets policy",

        // EN — hours/location/parking/contact
        "hours","open","close","open today","open now","weekend hours","holiday hours",
        "address","location","directions","nearby","parking","parking validation","where to park",
        "contact","phone number","email","website","social media",

        // EN — payments/gift cards/offers (still FAQ)
        "payment","pay","card","credit card","debit card","cash","apple pay","google pay","tap to pay",
        "gift card","gift certificate","voucher","coupon","groupon","discount","student discount",

        // RU — цены/политики/общая информация
        "цена","стоимост*","сколько стоит","сколько будет","прайс","меню услуг","прейскурант",
        "залог","предоплата","невозврат","не возвращается","политика отмен","политика опозданий",
        "штраф за неявку","no show","grace период","льготный период",
        "политика для новых клиентов","дети допускаются","с питомцами можно",

        // RU — режим/адрес/парковка/контакты
        "часы работы","график","режим","вы открыты","вы сегодня открыты","по выходным",
        "адрес","локация","как доехать","как добраться","парковка","где парковаться",
        "контакты","телефон","почта","сайт","соцсети",

        // RU — оплата/подарки/скидки
        "оплата","оплата картой","принимаете карту","наличными","apple pay","google pay",
        "подарочный сертификат","подарочная карта","купон","скидка","студенческая скидка",

        // ES — precios/políticas/info general
        "precio","cuánto cuesta","tarifa","presupuesto","lista de precios","menú de servicios",
        "depósito","anticipo","no reembolsable","no reembolsables",
        "política de cancelación","política de tardanza","no show","no asistir","periodo de gracia",
        "política nuevos clientes","política niños","política mascotas",

        // ES — horario/ubicación/parking/contacto
        "horario","abren","abierto","cerrado","hoy abren","fin de semana","festivos",
        "dirección","ubicación","cómo llegar","indicaciones","estacionamiento","dónde estacionar",
        "contacto","teléfono","correo","sitio web","redes sociales",

        // ES — pagos/regalos/ofertas
        "pago","tarjeta","efectivo","apple pay","google pay","tap to pay",
        "gift card","certificado de regalo","cupón","descuento","descuento estudiante",
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
            foreach (var raw in rawList)
            {
                // Skip empty lines or whitespace
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                // Normalize text (lowercase + remove diacritics)
                var needle = Normalize(raw);

                // Multi-word phrase → store in phrases list
                if (needle.Contains(' '))
                {
                    phrases.Add(needle.Trim());
                }
                // Word ends with '*' → store as prefix (stem)
                else if (needle.EndsWith('*'))
                {
                    var stem = needle.AsSpan(0, needle.Length - 1).ToString();
                    if (!string.IsNullOrWhiteSpace(stem) && stem.Length >= 3)
                        prefixes.Add(stem);
                }
                // Regular single word → store in words set
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
        var norm = Normalize(SplitterChars.Replace(text, " "));

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
    private const int PHRASE_WEIGHT = 3;

    // Decision thresholds for intent validation:
    // - REJECT_THRESHOLD: below this confidence → intent is Unknown (reject guess)
    // - CONFIRM_THRESHOLD: above this confidence → accept intent without clarification
    // - MIN_SCORE: minimum evidence required before considering an intent valid
    // - MIN_GAP: best score must exceed second best by at least this much (avoid ties)
    private const double REJECT_THRESHOLD = 0.50;
    private const double CONFIRM_THRESHOLD = 0.65;
    private const int MIN_SCORE = 2;
    private const int MIN_GAP = 1;

    private static (int score, int hits, string details) ScoreIntent
        (string normalizedText, HashSet<string> tokenSet, LexiconRuntime lx)
    {
        // Accumulates the total weight of all matched words/phrases
        int score = 0;
        // Counts how many matches were found
        int hits = 0;
        // Explaining how that score was produced
        var detailList = new List<string>(4);

        // 1) Exact word matches
        int wordHits = 0;
        foreach (var w in lx.Words)
        {
            if (tokenSet.Contains(w))
                wordHits++;
        }

        if (wordHits > 0)
        {
            score += wordHits * WORD_WEIGHT;
            hits += wordHits;
            detailList.Add($"words:{wordHits}");
        }

        // 2) Prefix matches
        int prefHits = 0;
        if( lx.PrefixWords.Length > 0)
        {
            // To avoid double counting, set needs to exclude exact words already matched
            var nonWordTokenz = lx.Words.Count > 0 ? tokenSet.Except(lx.Words) : tokenSet;

            // Loop through each token not already matched as an exact word
            foreach (var tok in nonWordTokenz)
            {
                // Check each prefix for a match againt the prefix dictionary
                foreach (var pref in lx.PrefixWords)
                {
                    // If the prefix word(token) less then 3 chars, skip it
                    if (pref.Length < 3)
                        continue;

                    if (tok.StartsWith(pref, StringComparison.Ordinal))
                    {
                        prefHits++;
                        break; // Move to next token after first prefix match
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

        // 3) Phrase matches
        int phraseHits = 0;
        foreach (var p in lx.Phrases)
            // 
            if (ContainsPhraseWithBoundaries(normalizedText, p)) phraseHits++;

        if (phraseHits > 0)
        {
            score += PHRASE_WEIGHT * phraseHits; // <— uses weight
            hits += phraseHits;                 // keep hits as “count of matches”
            detailList.Add($"phrases:{phraseHits}");
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
    private static string Normalize(string s)
    {
        var lower = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
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