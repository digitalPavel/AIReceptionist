using Demo1.Models;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Rule-based NLU for better intent understanding and performance.
/// Finds user intent via weighted scoring of words, prefixes, and phrases with priorities and confidence thresholds.
/// Helps avoid extra LLM usage to save cost.
/// </summary>

namespace Demo1.Services.Brain;

public static class IntentRules
{
    // Defined words/phrases for intent recognition. Use * as a wildcard for stemming.

    // BOOK — create a new appointment
    private static readonly string[] BookWords =
    {
        // ==============================
        // EN — booking (keywords)
        // ==============================
        "book","booking","schedule","make appointment","set up appointment",
        "available today","available now","next available","earliest available","time slot","slot",
        "new client","first time","consult","consultation","free consult",

        // ==============================
        // EN — booking (PHRASES - strong)
        // ==============================
        "i wanna make an appointment",
        "i wanna book an appointment",
        "i wanna schedule an appointment",
        "may i have an appointment",
        "i need an appointment",
        "can i get an appointment",
        "can you schedule an appointment for me",
        "create an appointment for me",
        "can i create an appointment",
        "i want to book an appointment",
        "i'd like to book an appointment",
        "i would like to book",
        "can i book an appointment",
        "can i make an appointment",
        "can you book me",
        "please book me",
        "i want to schedule an appointment",
        "i'd like to schedule",
        "can i schedule an appointment",
        "schedule an appointment",
        "book me for today",
        "book me for tomorrow",
        "book me for next week",
        "do you have anything today",
        "do you have any openings today",
        "any availability today",
        "next available appointment",
        "earliest available appointment",
        "first available appointment",
        "book a consultation",
        "book a free consultation",
        "book a haircut",
        "book highlights",
        "book balayage",
        "book keratin",
        "book extensions",

        // ==============================
        // RU — запись (keywords)
        // ==============================
        "запис*","бронь","забронировать","назначить приём","назначить прием","встать на запись",
        "свободные слоты","ближайшее время","самое раннее","новый клиент","консультация",

        // ==============================
        // RU — запись (PHRASES - strong)
        // ==============================
        "хочу записаться на приём",
        "хочу записаться на прием",
        "можно записаться на",
        "можно записаться сегодня",
        "можно записаться завтра",
        "запишите меня сегодня",
        "запишите меня на завтра",
        "запишите меня на следующую неделю",
        "есть что-то сегодня",
        "есть свободные окна сегодня",
        "ближайшая запись",
        "самое раннее время",
        "запишите меня на консультацию",
        "первичная консультация",
        "записаться как новый клиент",
        "впервые записаться",
        "записаться на стрижку",
        "записаться на окрашивание",
        "записаться на балаяж",
        "записаться на кератин",
        "записаться на укладку",
        "записаться на наращивание",

        // ==============================
        // ES — cita / agendar (keywords)
        // ==============================
        "cita","reserv*","agend*","turno","apart*","disponibilidad","disponible hoy",
        "nuevo cliente","primera vez","consulta","consultar",

        // ==============================
        // ES — cita / agendar (PHRASES - strong)
        // ==============================
        "quiero agendar una cita",
        "quiero hacer una cita",
        "puedo agendar una cita",
        "puedo hacer una cita",
        "me puede agendar una cita",
        "quisiera una cita",
        "quiero una cita hoy",
        "tienen citas hoy",
        "tienen disponibilidad hoy",
        "la próxima disponibilidad",
        "la cita más temprano",
        "primera cita disponible",
        "agendar una consulta",
        "cita para nuevo cliente",
        "cita para corte",
        "cita para color",
        "cita para balayage",
        "cita para queratina",
        "cita para peinado",
        "cita para extensiones",
        "quiero agendar para mañana",
        "puedo agendar para la próxima semana"
    };


    // RESCHEDULE — change existing appointment
    private static readonly string[] RescheduleWords =
    {
        // ==============================
        // EN — reschedule (keywords)
        // ==============================
        "reschedule","reschedul*","rebook",
        "change time","change appointment","change my appointment","change the appointment",
        "move appointment","move my appointment","move to another time","move to another day",
        "switch time","different time","another time","another day",
        "earlier time","later time","push back","bring forward","new time","adjust time",

        // ==============================
        // EN — reschedule (PHRASES - strong)
        // ==============================
        "reschedule my appointment",
        "can you reschedule my appointment",
        "could you reschedule my appointment",
        "can we reschedule my appointment",
        "please reschedule my appointment",
        "i can’t come today can you reschedule my appointment",
        "i cannot come today can you reschedule my appointment",
        "can you reschedule it for tomorrow",
        "can you move my appointment to tomorrow",
        "can you move my appointment to next week",
        "i’m running late can you reschedule my appointment",
        "i need to reschedule my appointment",
        "can i reschedule my appointment",
        "i want to reschedule my appointment",
        "please reschedule my appointment",
        "i'd like to reschedule",
        "can you change my appointment",
        "can i change my appointment time",
        "can i move my appointment",
        "can you move my appointment",
        "move my appointment to tomorrow",
        "move my appointment to next week",
        "reschedule for tomorrow",
        "reschedule for next week",
        "same time tomorrow",
        "any time tomorrow",
        "book me for a different time",
        "rebook me for another day",
        "can i change it to later",
        "can i move it earlier",
        "push my appointment back a bit",
        "bring it forward a bit",

        // ==============================
        // RU — перенос (keywords)
        // ==============================
        "перенес*","перезапис*","сдвин*","перекин*","перемест*","заменить время","поменять время","изменить время","новое время",
        "на другое время","на другой день","пораньше","попозже",

        // ==============================
        // RU — перенос (PHRASES - strong)
        // ==============================
        "могу ли я перенести запись",
        "хочу перенести запись",
        "нужно перенести запись",
        "перенесите меня пожалуйста",
        "можно перенести на завтра",
        "можно перенести на следующую неделю",
        "перенесите на завтра",
        "перенесите на следующий день",
        "перезапишите меня на другое время",
        "запишите меня на другое время",
        "перенести на утро",
        "перенести на вечер",
        "в это же время завтра",
        "сменить время записи",
        "заменить время записи",

        // ==============================
        // ES — reprogramar / cambiar cita (keywords)
        // ==============================
        "reprogram*","cambiar hora","cambiar cita","cambiar mi cita","mover cita",
        "otra hora","otro día","posponer","adelantar","nuevo horario","nuevo día",

        // ==============================
        // ES — reprogramar / cambiar cita (PHRASES - strong)
        // ==============================
        "necesito reprogramar mi cita",
        "quiero reprogramar mi cita",
        "puedo reprogramar mi cita",
        "me puede cambiar la cita",
        "quiero cambiar la hora de mi cita",
        "puedo mover mi cita",
        "puede mover mi cita",
        "mover mi cita para mañana",
        "mover mi cita para la próxima semana",
        "reprogramar para mañana",
        "reprogramar para la próxima semana",
        "la misma hora mañana",
        "cambiarla para más tarde",
        "cambiarla para más temprano"
    };


    private static readonly string[] CancelWords =
    {
        // ==============================
        // EN — cancel (keywords)
        // ==============================
        "cancel","cancel appointment","cancel my appointment","cancel the appointment",
        "cancel booking","cancel my booking","cancel appt","cancel my appt",
        "call off","no longer need","don’t need anymore","do not need anymore",
        "can’t make it","cant make it","cannot make it","won’t make it","wont make it",
        "not coming","running late","emergency","family emergency","sick","sick today",
        "traffic delay",

        // ==============================
        // EN — cancel (PHRASES - strong)
        // ==============================
        "i need to cancel my appointment",
        "i have to cancel my appointment",
        "please cancel my appointment",
        "can you cancel my appointment",
        "could you cancel my appointment",
        "i want to cancel my appointment",
        "i do not need the appointment anymore",
        "i can’t come please cancel",
        "i cannot make it please cancel",
        "i’m sick please cancel my appointment",
        "i have an emergency please cancel",
        "i’m running late cancel my appointment",
        "cancel my booking please",
        "drop the appointment please",
        "remove my booking please",

        // ==============================
        // RU — отмена (keywords)
        // ==============================
        "отмена","отменить запись","отмена записи","удалите запись",
        "убрать запись","снять запись","отказаться от записи",
        "передумал","неактуально","больше не надо",
        "не смогу прийти","не смогу","не получится прийти","не получится",
        "заболел","приболел","болею","плохо себя чувствую",
        "не приду","опаздываю","форс мажор","семейные обстоятельства",

        // ==============================
        // RU — отмена (PHRASES - strong)
        // ==============================
        "хочу отменить запись",
        "нужно отменить запись",
        "отмените мою запись пожалуйста",
        "можете отменить мою запись",
        "я передумал отмените запись",
        "не смогу прийти отмените запись",
        "я заболел отмените запись",
        "у меня форс мажор отмените запись",
        "опаздываю отмените запись",

        // ==============================
        // ES — cancelar (keywords)
        // ==============================
        "cancelar","cancelar la cita","cancelar mi cita","anular cita","anular la cita",
        "ya no la necesito","no la necesito","no puedo ir","no podré asistir",
        "emergencia familiar","enfermo","enferma","estoy enfermo","estoy enferma",

        // ==============================
        // ES — cancelar (PHRASES - strong)
        // ==============================
        "necesito cancelar mi cita",
        "tengo que cancelar mi cita",
        "por favor cancela mi cita",
        "puede cancelar mi cita",
        "quiero cancelar mi cita",
        "no puedo ir por favor cancela",
        "estoy enfermo cancela mi cita",
        "tengo una emergencia cancela mi cita"
    };


    // HANDOFF — request for human / language escalation
    private static readonly string[] HandoffWords =
      {
        // ==============================
        // EN — talk to human (keywords)
        // ==============================
        "operator","human","representative","agent","live agent","real person","receptionist",
        "front desk","staff","manager","someone","support","help","person",

        // EN — direct human requests (PHRASES - strong)
        "speak to a human",
        "talk to a human",
        "talk to someone",
        "speak to someone",
        "speak to a representative",
        "talk to a representative",
        "connect me to a human",
        "transfer me to a human",
        "connect me please",
        "transfer me please",
        "can i talk to someone",
        "can i speak to a person",
        "i need a person",
        "i want to talk to a person",
        "human please",
        "someone else please",
        "let me talk to someone",
        "can you transfer me to front desk",
        "can you connect me to reception",
        "can you connect me to the manager",

        // EN — frustration / escalation (PHRASES)
        "this is not helping",
        "i do not understand",
        "not working",
        "stop the robot",
        "stop talking",
        "i need assistance",
        "i need help",
        "let me talk to a real person",
        "connect me with support",

        // EN — language routing (PHRASES)
        "english please",
        "spanish please",
        "russian please",
        "spanish speaker",
        "russian speaker",
        "do you speak russian",
        "do you speak spanish",
        "i need spanish",
        "i need russian",
        "help in spanish",
        "help in russian",

        // ==============================
        // RU — человек нужен (keywords)
        // ==============================
        "оператор","консультант","администратор","менеджер","человек","помощь",
        "живой человек","настоящий человек","менеджер","рецепция",

        // RU — человек нужен (PHRASES - strong)
        "соедините с оператором",
        "переключите на оператора",
        "переведите на оператора",
        "соедините меня с человеком",
        "хочу поговорить с человеком",
        "можно с человеком",
        "нужна помощь оператора",
        "нужен менеджер",
        "не понимаю",
        "это не помогает",
        "нужна помощь",
        "помогите пожалуйста",
        "можно по-русски",
        "говорите по-русски",
        "говорит по-русски",
        "помощь по-русски",
        "русский язык пожалуйста",

        // ==============================
        // ES — humano / idioma (keywords)
        // ==============================
        "humano","agente","representante","operador","persona real","recepcionista","encargado",
        "ayuda","soporte","asistencia","manager","gerente",

        // ES — hablar con humano (PHRASES - strong)
        "hablar con alguien",
        "hablar con un representante",
        "hablar con una persona",
        "hablar con humano",
        "conéctame con alguien",
        "transferirme con alguien",
        "conectar con humano",
        "esto no ayuda",
        "no entiendo",
        "necesito ayuda",
        "necesito asistencia",
        "en español por favor",
        "habla español",
        "hablan español",
        "puedo hablar español",
        "necesito español",
        "ayuda en español",
        "en ruso por favor",
        "habla ruso",
        "hablan ruso",
        "ayuda en ruso"
    };


    // FAQ — pricing, hours, policies, location, payments, service info
    private static readonly string[] FaqWords =
    {
        // ==============================
        // EN — pricing (keywords)
        // ==============================
        "price","prices","pricing","cost","how much","fee","fees","estimate","quote",
        "price list","service menu","menu","rates","discount","coupon","specials",
        "starting at","from price","price range","consultation fee",

        // EN — services (keywords often used with price)
        "haircut","trim","fade","layers","bangs","fringe",
        "color","dye","bleach","highlights","lowlights","balayage","toner","ombre",
        "blowout","style","keratin","extensions","braids",

        // ==============================
        // EN — hours & availability (keywords)
        // ==============================
        "hours","business hours","opening hours","store hours","salon hours",
        "what time do you open","when do you open","what time do you close","when do you close",
        "open today","open now","closed now",
        "walk-ins welcome","do you take walk-ins",
        "availability","next availability","next opening",
        "weekend hours","weekday hours","holiday hours","are you open on","are you open today",
        "when you opened","when you closed", // fixed typo

        // ==============================
        // EN — location & contact (keywords)
        // ==============================
        "address","location","directions","how do i get there","near me","parking","where to park",
        "phone number","contact","website","instagram","facebook",

        // ==============================
        // EN — policies & payments (keywords)
        // ==============================
        "new client policy","cancellation policy","late policy","no show fee",
        "deposit","prepayment","refund policy","no refunds",
        "credit card","debit card","cash only","card only","tap to pay",
        "apple pay","google pay","zelle","venmo","cash app","gratuity included",

        // ==============================
        // EN — pricing/hours/payments (PHRASES - strong)
        // ==============================
        "tell me about your prices",
        "can you tell me your prices",
        "how much does it cost",
        "how much do you charge",
        "how much is a haircut",
        "how much is keratin",
        "how much is balayage",
        "price for a haircut",
        "what are your prices",
        "what is the price for",
        "what are your hours",
        "what time are you open until",
        "are you open today",
        "are you open now",
        "are you open right now",
        "do you accept apple pay",
        "do you take apple pay",
        "do you accept card",
        "do you accept credit cards",
        "do you take cards",
        "do you take cash",
        "do you accept cash",
        "do you accept google pay",
        "do you accept zelle",
        "do you accept venmo",
        "do you accept cash app",
        "your hours", // short but useful phrase

        // ==============================
        // RU — цены / меню / услуги (keywords)
        // ==============================
        "цена","цены","стоимость","сколько стоит","сколько будет","прайс","меню услуг","прейскурант",
        "консультация","бесплатная консультация",
        "стрижка","окрашивание","балаяж","мелирование","укладка","кератин","наращивание","пряди",

        // RU — часы / доступность / адрес (keywords)
        "часы работы","график","режим работы","расписание",
        "во сколько открываетесь","во сколько закрываете","вы сегодня открыты","сейчас открыты",
        "без записи","есть запись сегодня",
        "адрес","локация","как добраться","рядом","парковка","телефон","сайт","инстаграм","фейсбук",

        // RU — политики / оплата (keywords)
        "залог","предоплата","политика отмен","штраф за неявку","возврат средств","без возвратов",
        "оплата","картой","наличными","apple pay","google pay","чаевые включены",

        // RU — цены/часы/оплата (PHRASES)
        "какие у вас цены",
        "расскажите про цены",
        "сколько стоит стрижка",
        "сколько стоит окрашивание",
        "сколько стоит кератин",
        "какая стоимость услуги",
        "какие у вас часы работы",
        "во сколько вы открываетесь",
        "во сколько вы закрываетесь",
        "вы открыты сегодня",
        "вы сейчас открыты",
        "принимаете ли вы карты",
        "принимаете ли вы apple pay",
        "принимаете ли вы наличные",
        "можно оплатить картой",
        "можно оплатить наличными",

        // ==============================
        // ES — precios / servicios (keywords)
        // ==============================
        "precio","precios","cuánto cuesta","lista de precios","menú de servicios",
        "corte","color","balayage","queratina","extensiones",
        "consulta","consulta gratis",

        // ES — horario / acceso (keywords)
        "horario","a qué hora abren","a qué hora cierran","están abiertos hoy",
        "abierto ahora","cerrado ahora",
        "sin cita","cita el mismo día","walk in",
        "dirección","ubicación","cómo llegar","cerca de mí","estacionamiento","validación de estacionamiento",
        "teléfono","sitio web","redes sociales","instagram","facebook",

        // ES — políticas / pago (keywords)
        "política de cancelación","cargo por tardanza","cargo por no show","política nuevos clientes",
        "depósito","anticipo","reembolso","sin reembolsos",
        "tarjeta","efectivo","solo efectivo","apple pay","google pay","propina incluida",

        // ES — precios/horario/pagos (PHRASES)
        "cuáles son sus precios",
        "cuánto cuesta el corte",
        "cuánto cuesta la queratina",
        "me puede decir sus precios",
        "cuál es su horario",
        "están abiertos hoy",
        "están abiertos ahora",
        "aceptan tarjeta",
        "aceptan apple pay",
        "aceptan efectivo",
        "aceptan google pay",
        "aceptan zelle",
        "aceptan venmo"
    };

    // Neutral tokens that do not contribute to intent scoring
    private static readonly HashSet<string> NeutralTokens = new(StringComparer.Ordinal)
    {
        // ==============================
        // EN — polite / connectors / fillers
        // ==============================
        "hi","hello","hey","yo","good morning","good afternoon","good evening",
        "thanks","thank you","thank","please","welcome",
        "ok","okay","alright","sure","fine","cool","great","nice","awesome",
        "no problem","all good","sounds good","that’s fine","that’s okay",
        "yes","no","maybe","k","yep","yeah","nah","mmhm","uh","um","well","right","really","actually","so","just","like","also","then","anyway","oh","hmm","uhh",

        // Soft temporal / contextual (non-directive)
        "today","tomorrow","morning","afternoon","evening","tonight","soon","later","now",
        "this","next","day","time","week","month",

        // ==============================
        // RU — нейтральные / вежливые
        // ==============================
        "привет","здравствуйте","хай","ок","ладно","ага","угу",
        "спасибо","пожалуйста","всё хорошо","все хорошо","нормально","ничего","ясно","понятно",
        "да","нет","можно","ну","так","вроде","как бы","ладненько","ага ладно","окей","хорошо","замечательно",
        "позже","скоро","сейчас","потом","утром","днем","вечером","завтра","сегодня",

        // ==============================
        // ES — neutrales / amables
        // ==============================
        "hola","buenas","buenos días","buenas tardes","buenas noches",
        "gracias","de nada","por favor","ok","vale","claro","perfecto","bien","genial",
        "sí","si","no","tal vez","quizás","quizas","ajá","mmm","eh","bueno","pues","entonces","así","también","ahora","luego","pronto","después","hoy","mañana","tarde","noche"
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