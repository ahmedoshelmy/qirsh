using Microsoft.Extensions.Time.Testing;
using Qirsh.Contracts;
using Qirsh.Ledger.Application;
using Qirsh.Ledger.Domain;

namespace Qirsh.Ledger.UnitTests;

public sealed class LedgerHandlerTests
{
    private readonly FakeLedger _ledger = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    private RecordTransactionFromSms Recorder() => new(_ledger, _ledger, _ledger, _ledger, _ledger, _ledger, _clock);

    private RecategoriseTransaction Recategoriser() => new(_ledger, _ledger, _ledger, _ledger, _ledger, _ledger);

    private static SmsParsed Sms(string hash, string? merchant, decimal amount = 100m) => new(
        Guid.NewGuid(), hash, "CIB", "4821", MoneyDirection.Out, amount, "EGP", merchant, new DateOnly(2026, 11, 5), "Card", null, null);

    [Fact]
    public async Task Records_a_transaction_files_it_by_keyword_and_announces_it()
    {
        var outcome = await Recorder().HandleAsync(Sms("H1", "CARREFOUR MAADI"), default);

        outcome.ShouldBe(RecordOutcome.Recorded);
        var tx = _ledger.Transactions.ShouldHaveSingleItem();
        tx.Category.ShouldBe("Groceries");
        _ledger.Accounts.ShouldHaveSingleItem().Name.ShouldBe("CIB ****4821");
        _ledger.Published.ShouldHaveSingleItem().ShouldBeOfType<TransactionRecorded>().Category.ShouldBe("Groceries");
        _ledger.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task A_matching_rule_hit_is_counted_through_the_atomic_increment_and_only_for_new_transactions()
    {
        await Recorder().HandleAsync(Sms("H1", "CARREFOUR MAADI"), default);
        await Recorder().HandleAsync(Sms("H1", "CARREFOUR MAADI"), default);
        await Recorder().HandleAsync(Sms("H2", "UNKNOWN SHOP"), default);

        _ledger.RuleHits.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Guid, int>(_ledger.Rules[0].Id, 1));
    }

    [Fact]
    public async Task The_same_sms_on_the_same_account_is_recorded_once()
    {
        await Recorder().HandleAsync(Sms("H1", "CARREFOUR MAADI"), default);
        var second = await Recorder().HandleAsync(Sms("H1", "CARREFOUR MAADI"), default);

        second.ShouldBe(RecordOutcome.Duplicate);
        _ledger.Transactions.Count.ShouldBe(1);
        _ledger.Published.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Correcting_a_category_learns_the_merchant_for_next_time()
    {
        await Recorder().HandleAsync(Sms("H1", "CARREFOUR MAADI"), default);
        var first = _ledger.Transactions[0];

        var moved = await Recategoriser().HandleAsync(new RecategoriseCommand(first.Id, "Shopping"), default);
        await Recorder().HandleAsync(Sms("H2", "Carrefour  Maadi"), default);

        moved.ShouldBe(RecategoriseOutcome.Moved);
        _ledger.Rules.ShouldContain(r => r.Source == RuleSource.Learned && r.Pattern == "CARREFOUR MAADI" && r.Category == "Shopping");
        _ledger.Transactions[1].Category.ShouldBe("Shopping");
        var evt = _ledger.Published.OfType<TransactionRecategorised>().ShouldHaveSingleItem();
        evt.FromCategory.ShouldBe("Groceries");
        evt.ToCategory.ShouldBe("Shopping");
    }

    [Fact]
    public async Task A_second_correction_overrides_the_learned_rule()
    {
        await Recorder().HandleAsync(Sms("H1", "KIOSK 9"), default);
        var tx = _ledger.Transactions[0];

        await Recategoriser().HandleAsync(new RecategoriseCommand(tx.Id, "Shopping"), default);
        await Recategoriser().HandleAsync(new RecategoriseCommand(tx.Id, "Groceries"), default);

        _ledger.Rules.Where(r => r.Source == RuleSource.Learned).ShouldHaveSingleItem().Category.ShouldBe("Groceries");
    }

    [Fact]
    public async Task Recategorise_rejects_unknown_categories_and_missing_transactions()
    {
        await Recorder().HandleAsync(Sms("H1", "KIOSK 9"), default);

        (await Recategoriser().HandleAsync(new RecategoriseCommand(_ledger.Transactions[0].Id, "Nope"), default)).ShouldBe(RecategoriseOutcome.UnknownCategory);
        (await Recategoriser().HandleAsync(new RecategoriseCommand(Guid.NewGuid(), "Shopping"), default)).ShouldBe(RecategoriseOutcome.NotFound);
    }

    [Fact]
    public async Task Moving_to_the_same_category_changes_nothing()
    {
        await Recorder().HandleAsync(Sms("H1", "CARREFOUR"), default);

        var outcome = await Recategoriser().HandleAsync(new RecategoriseCommand(_ledger.Transactions[0].Id, "Groceries"), default);

        outcome.ShouldBe(RecategoriseOutcome.Unchanged);
        _ledger.Published.OfType<TransactionRecategorised>().ShouldBeEmpty();
    }
}
