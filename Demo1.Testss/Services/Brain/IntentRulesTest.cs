using Demo1.Models;
using Demo1.Services.Brain;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Demo1.Tests.Services.Brain
{
    public class IntentRulesTests_HairSalon
    {
        // --------- Reflection bridges (same style as your helpers) ---------

        private static readonly Type IntentRulesType = typeof(IntentRules);

        private static MethodInfo GetPrivateMethod(string name)
        {
            var mi = IntentRulesType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            return mi!;
        }

        private static T InvokePrivate<T>(string name, params object?[] args)
        {
            var mi = GetPrivateMethod(name);
            var result = mi.Invoke(null, args);
            return (T)result!;
        }

        private static string Normalize(string s) => InvokePrivate<string>("Normalize", s);

        private static bool ContainsPhraseWithBoundaries(string haystack, string needle)
            => InvokePrivate<bool>("ContainsPhraseWithBoundaries", haystack, needle);

        private static object GetLexiconFor(Intent intent)
        {
            var field = IntentRulesType.GetField("_lex", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            var dict = field!.GetValue(null)!;
            var indexer = dict.GetType().GetProperty("Item");
            Assert.NotNull(indexer);
            return indexer!.GetValue(dict, new object[] { intent })!;
        }

        private static (int score, int hits, string details) ScoreIntent(string normalizedText, HashSet<string> tokenSet, Intent intent)
        {
            var lx = GetLexiconFor(intent);
            var mi = IntentRulesType.GetMethod("ScoreIntent", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            var result = mi!.Invoke(null, new object?[] { normalizedText, tokenSet, lx })!;
            var t = ((ValueTuple<int, int, string>)result);
            return (t.Item1, t.Item2, t.Item3);
        }

        // ===========================
        // BOOK
        // ===========================

        [Theory]
        [InlineData("I’d like to book a haircut for today.")]
        [InlineData("can i book an appointment?")]
        [InlineData("same day appointment available?")]
        [InlineData("walk-in appointment now")]
        [InlineData("book balayage next available")]
        public void Book_EN_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Book, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("Хочу записаться на стрижку, есть свободные слоты сегодня?")]
        [InlineData("Можно записаться на консультацию на завтра утром?")]
        [InlineData("Впервые записаться к вам на окрашивание")]
        public void Book_RU_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Book, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("Quiero una cita para color, ¿tienen disponibilidad hoy?")]
        [InlineData("¿Puedo agendar una cita para balayage mañana por la mañana?")]
        [InlineData("Necesito una cita lo antes posible")]
        public void Book_ES_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Book, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        // ===========================
        // RESCHEDULE
        // ===========================

        [Theory]
        [InlineData("I need to reschedule my appointment to tomorrow afternoon")]
        [InlineData("can you move my appointment to next week?")]
        [InlineData("push it back one hour")]
        public void Reschedule_EN_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Reschedule, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("Давайте перенесём запись на завтра во второй половине дня")]
        [InlineData("Можно поменять время на более позднее?")]
        [InlineData("Перезаписать меня на понедельник")]
        public void Reschedule_RU_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Reschedule, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("¿Podemos reprogramar la cita para el lunes?")]
        [InlineData("Necesito cambiar la hora a mañana por la tarde")]
        [InlineData("¿Se puede mover mi cita a una fecha posterior?")]
        public void Reschedule_ES_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Reschedule, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        // ===========================
        // CANCEL
        // ===========================

        [Theory]
        [InlineData("please cancel my appointment")]
        [InlineData("i need to cancel it")]
        [InlineData("cancel the booking asap")]
        public void Cancel_EN_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Cancel, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("Пожалуйста, отмените мою запись")]
        [InlineData("Хочу отменить визит")]
        [InlineData("Отмена записи")]
        public void Cancel_RU_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Cancel, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("Por favor, cancela mi cita")]
        [InlineData("Quiero cancelar la reserva")]
        [InlineData("Cancelar la cita de hoy")]
        public void Cancel_ES_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Cancel, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        // ===========================
        // FAQ
        // ===========================

        [Theory]
        [InlineData("what are your prices and weekend hours?")]
        [InlineData("do you have a price list?")]
        [InlineData("are you open on saturday? what time?")]
        [InlineData("when you clsoed on holidays?")] // misspelling recall
        public void Faq_EN_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Faq, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("Сколько стоит консультация и вы работаете по выходным?")]
        [InlineData("Есть прайс-лист?")]
        [InlineData("Какие у вас часы работы в субботу?")]
        public void Faq_RU_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Faq, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("¿Tienen lista de precios y horario de fin de semana?")]
        [InlineData("¿Cuál es el horario del sábado?")]
        [InlineData("¿Aceptan tarjeta y apple pay?")]
        public void Faq_ES_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Faq, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        // ===========================
        // HANDOFF
        // ===========================

        [Theory]
        [InlineData("transfer me to a live agent")]
        [InlineData("connect me to a human representative")]
        [InlineData("this is not helping, i need a person")]
        public void Handoff_EN_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Handoff, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
            Assert.Contains("Handoff", r.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Переключите меня на живого оператора, пожалуйста")]
        [InlineData("Соедините с человеком")]
        [InlineData("Это не помогает, нужен менеджер")]
        public void Handoff_RU_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Handoff, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        [Theory]
        [InlineData("Pásame con un agente en vivo, por favor")]
        [InlineData("¿Puedes transferirme con una persona?")]
        [InlineData("Esto no ayuda, necesito ayuda de un humano")]
        public void Handoff_ES_Positive(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Handoff, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        // ===========================
        // PRIORITY & DISAMBIGUATION
        // ===========================

        [Fact]
        public void Priority_Handoff_Overrides_Faq()
        {
            // Contains FAQ tokens, but explicit handoff must win
            var text = "what are your prices? also connect me to a human, please";
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Handoff, r.Intent);
        }

        [Fact]
        public void Priority_Book_Overrides_Faq_WhenBothPresent()
        {
            // Price mention + explicit booking phrase: booking should win
            var text = "price list please, and i’d like to book an appointment for a haircut";
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Book, r.Intent);
        }

        [Fact]
        public void Unknown_WhenWeakAndConflicting()
        {
            // Weak signals for multiple intents should fall back to Unknown
            var text = "maybe book later or change time, not sure";
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Unknown, r.Intent);
        }

        // ===========================
        // NEUTRALS & NORMALIZATION
        // ===========================

        [Theory]
        [InlineData("hi, hello, good evening, please")]
        [InlineData("ok thanks, today or tomorrow maybe")]
        public void NeutralTokens_DoNotTriggerIntent(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Unknown, r.Intent);
        }

        [Fact]
        public void Normalize_CollapsesWhitespace_RemovesPunct()
        {
            var input = "  Book \t an  \r\n appointment!!!  ";
            var norm = Normalize(input);
            Assert.Equal("book an appointment", norm);
            Assert.True(ContainsPhraseWithBoundaries(norm, "book an appointment"));
        }

        [Fact]
        public void Normalize_StripsDiacritics_Spanish()
        {
            var input = "¿Están abiertos el Sábado? Necesito una Citá.";
            var norm = Normalize(input);
            Assert.DoesNotContain('á', norm);
            Assert.Contains("estan abiertos el sabado", norm);
            Assert.Contains("cita", norm);
        }

        // ===========================
        // BOUNDARIES & TOKENIZATION
        // ===========================

        [Theory]
        [InlineData("we are rebooking tomorrow")] // "book" inside another word
        [InlineData("preappointment checks are complete")] // "appointment" embedded
        public void InsideWord_Matches_DoNotCount(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Unknown, r.Intent);
        }

        [Theory]
        [InlineData("book/appointment now")]
        [InlineData("book.appointment now")]
        [InlineData("book_appointment now")]
        public void Splitters_SlashesDotsUnderscores_PhraseStillFound(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Book, r.Intent);
            Assert.True(r.Confidence >= 0.65, r.Reason);
        }

        // ===========================
        // SCORING INTERNALS
        // ===========================

        [Fact]
        public void PhraseScores_Override_WordSum_ForBook()
        {
            var phrase = "book an appointment";
            var norm = Normalize(phrase);
            var tokens = new HashSet<string>(StringComparer.Ordinal) { "book", "an", "appointment" };

            var (score, hits, details) = ScoreIntent(norm, tokens, Intent.Book);

            // Фраза даёт высокий вклад, даже если слова не считаются
            Assert.True(score >= 4, $"score={score}, details={details}");

            // Из-за блокировки токенов фразы общий hits = 1 (только сама фраза)
            Assert.Equal(1, hits);

            Assert.Contains("phrases", details, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("words:", details, StringComparison.OrdinalIgnoreCase); // опционально, чтобы зафиксировать поведение
        }


        [Fact]
        public void MixedTokens_Aggregate_Reschedule()
        {
            var norm = Normalize("reschedule please to tomorrow");
            var tokens = new HashSet<string>(StringComparer.Ordinal) { "reschedule", "please", "to", "tomorrow" };
            var (score, hits, details) = ScoreIntent(norm, tokens, Intent.Reschedule);

            Assert.True(score >= 1);
            Assert.True(hits >= 1);
            Assert.Contains("words", details, StringComparison.OrdinalIgnoreCase);
        }

        // ===========================
        // MISSPELLING RECALL
        // ===========================

        [Fact]
        public void Misspelling_Faq_WhenYouClsoed_MapsToFaq()
        {
            var r = IntentRules.ClassifyWithConfidence("when you clsoed on sunday?");
            Assert.Equal(Intent.Faq, r.Intent);
        }

        // ===========================
        // LANGUAGE ROUTING -> HANDOFF
        // ===========================

        [Theory]
        [InlineData("spanish please")]
        [InlineData("do you speak russian")]
        [InlineData("помощь по-русски")]
        [InlineData("en español por favor")]
        public void LanguageRequests_RouteToHandoff(string text)
        {
            var r = IntentRules.ClassifyWithConfidence(text);
            Assert.Equal(Intent.Handoff, r.Intent);
        }
    }
}
