using System;
using System.Linq;
using System.Threading.Tasks;
using Demo1.Models;
using Demo1.Services.Brain;
using Xunit;

namespace Demo1.Tests.Services.Brain
{
    public class StateStoreTests
    {
        [Fact]
        public void GetOrCreate_NewCallSid_CreatesWithDefaults()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-1";

            var st = store.GetOrCreate(sid);

            Assert.NotNull(st);
            Assert.Equal(sid, st.CallSid);
            Assert.Equal("en", st.Lang);
            Assert.Equal("America/New_York", st.TimeZoneId);
            Assert.Equal(Intent.Unknown, st.Intent);
            Assert.False(st.IsDone);
            Assert.Equal(0, st.RetryCount);
            Assert.Equal(Asked.None, st.Asked);
            Assert.Empty(st.PartyMembers);
            Assert.Equal(0, st.PartyIndex);
            Assert.False(st.SameTimeGroup);
            Assert.Null(st.GroupAnchorWhen);
            Assert.Empty(st.TakenMastersAtAnchor);
            Assert.False(st.ReadyToConfirm);
            Assert.NotNull(st.Slots);
            Assert.Null(st.Slots.Service);             // do not assume collections exist
        }

        [Fact]
        public void GetOrCreate_SameCallSid_ReturnsSameInstance()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-2";

            var a = store.GetOrCreate(sid);
            var b = store.GetOrCreate(sid);

            Assert.Same(a, b);
        }

        [Fact]
        public void Update_ExistingState_ReplacesWithNewRecord()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-3";

            var before = store.GetOrCreate(sid);

            store.Update(sid, s => s with
            {
                Lang = "ru",
                Intent = Intent.Book,
                RetryCount = s.RetryCount + 1,
                Slots = s.Slots with { Service = "haircut" }
            });

            var after = store.GetOrCreate(sid);

            // Verify reference replacement and new values
            Assert.NotSame(before, after);
            Assert.Equal("ru", after.Lang);
            Assert.Equal(Intent.Book, after.Intent);
            Assert.Equal(1, after.RetryCount);
            Assert.Equal("haircut", after.Slots.Service);

            // Snapshot immutability: original instance unchanged
            Assert.Equal("en", before.Lang);
            Assert.Equal(Intent.Unknown, before.Intent);
            Assert.Equal(0, before.RetryCount);
            Assert.Null(before.Slots.Service);
        }

        [Fact]
        public void Update_NonExisting_CreatesAndAppliesUpdater()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-4";

            store.Update(sid, s => s with { Intent = Intent.Cancel, Lang = "es" });

            var st = store.GetOrCreate(sid);
            Assert.Equal("es", st.Lang);
            Assert.Equal(Intent.Cancel, st.Intent);
        }

        [Fact]
        public void Remove_RemovesState()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-5";

            var first = store.GetOrCreate(sid);
            store.Update(sid, s => s with { RetryCount = 5 });

            store.Remove(sid);

            var fresh = store.GetOrCreate(sid);
            Assert.NotSame(first, fresh); // new instance created after removal
            Assert.Equal(0, fresh.RetryCount); // defaults restored
            Assert.Equal("en", fresh.Lang);
        }

        [Fact]
        public async Task Update_IsThreadSafe_ConcurrentIncrements()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-6";
            var ops = Enumerable.Range(0, 1_000)
                .Select(_ => Task.Run(() =>
                    store.Update(sid, s => s with { RetryCount = s.RetryCount + 1 })))
                .ToArray();

            await Task.WhenAll(ops);

            var st = store.GetOrCreate(sid);
            Assert.Equal(1_000, st.RetryCount);
        }

        [Fact]
        public void ArgumentValidation_NullKeys_ThrowArgumentNullException()
        {
            var store = new InMemoryStateStore();

            Assert.Throws<ArgumentNullException>(() => store.GetOrCreate(null!));
            Assert.Throws<ArgumentNullException>(() => store.Update(null!, s => s));
            Assert.Throws<ArgumentNullException>(() => store.Remove(null!));

            // updater null also throws ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => store.Update("sid", null!));
        }

        [Fact]
        public void KeyValidation_WhitespaceRejected()
        {
            var store = new InMemoryStateStore();
            Assert.Throws<ArgumentException>(() => store.GetOrCreate(" "));
            Assert.Throws<ArgumentException>(() => store.Update(" \t ", s => s));
            Assert.Throws<ArgumentException>(() => store.Remove("\r\n"));
        }

        [Fact]
        public void Update_NoOp_UpdaterReturnsSameInstance_DoesNotReplace()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-7";

            var before = store.GetOrCreate(sid);
            store.Update(sid, s => s); // no-op
            var after = store.GetOrCreate(sid);

            Assert.Same(before, after);
        }

        [Fact]
        public async Task Update_ConcurrentAcrossMultipleKeys_IsolatedCounters()
        {
            var store = new InMemoryStateStore();
            var sids = Enumerable.Range(1, 10).Select(i => $"CALL-{i}").ToArray();

            var tasks = Enumerable.Range(0, 10_000).Select(i =>
            {
                var sid = sids[i % sids.Length];
                return Task.Run(() => store.Update(sid, s => s with { RetryCount = s.RetryCount + 1 }));
            });

            await Task.WhenAll(tasks);

            foreach (var sid in sids)
            {
                var st = store.GetOrCreate(sid);
                Assert.Equal(1_000, st.RetryCount);
            }
        }

        [Fact]
        public void Update_NewKey_UpdaterThrows_DoesNotInsertPartialState()
        {
            var store = new InMemoryStateStore();
            var sid = "CALL-ERR";

            Assert.Throws<InvalidOperationException>(() =>
                store.Update(sid, _ => throw new InvalidOperationException("boom")));

            // Should create a fresh default now, proving no partial state exists
            var st = store.GetOrCreate(sid);
            Assert.Equal(0, st.RetryCount);
            Assert.Equal(Intent.Unknown, st.Intent);
        }
    }
}
