using FluentAssertions;
using NSubstitute;
using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Requests;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Requests;

public class SponsorshipTypeServiceTests
{
    private readonly ISponsorshipTypeRepository _repo = Substitute.For<ISponsorshipTypeRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly PassThroughCacheService _cache = new();

    private SponsorshipTypeService CreateSut() => new(_repo, _uow, _cache);

    [Fact]
    public async Task ListAsync_active_only_passes_flag_through_to_repository()
    {
        _repo.ListAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<SponsorshipType> { TestData.SponsorshipType(1, "Event") });

        var result = await CreateSut().ListAsync(activeOnly: true);

        result.Should().ContainSingle(t => t.Name == "Event");
        await _repo.Received(1).ListAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_all_includes_inactive_types()
    {
        _repo.ListAsync(false, Arg.Any<CancellationToken>())
            .Returns(new List<SponsorshipType>
            {
                TestData.SponsorshipType(1, "Event"),
                TestData.SponsorshipType(2, "Retired", active: false)
            });

        var result = await CreateSut().ListAsync(activeOnly: false);

        result.Should().HaveCount(2);
        result.Should().Contain(t => !t.IsActive);
    }

    [Fact]
    public async Task CreateAsync_adds_type_saves_and_evicts_cache()
    {
        var dto = new CreateSponsorshipTypeDto("Charity");

        var result = await CreateSut().CreateAsync(dto);

        result.Name.Should().Be("Charity");
        result.IsActive.Should().BeTrue("new types default to active");
        await _repo.Received(1).AddAsync(
            Arg.Is<SponsorshipType>(t => t.Name == "Charity"), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _cache.RemovedPrefixes.Should().Contain("sponsorship-types:");
    }

    [Fact]
    public async Task UpdateAsync_missing_type_throws_NotFound()
    {
        _repo.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((SponsorshipType?)null);

        var act = () => CreateSut().UpdateAsync(99, new UpdateSponsorshipTypeDto("X", true));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_renames_and_deactivates_then_evicts_cache()
    {
        var entity = TestData.SponsorshipType(5, "Old", active: true);
        _repo.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(entity);

        var result = await CreateSut().UpdateAsync(5, new UpdateSponsorshipTypeDto("New", IsActive: false));

        result.Name.Should().Be("New");
        result.IsActive.Should().BeFalse();
        entity.Name.Should().Be("New");
        entity.IsActive.Should().BeFalse();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _cache.RemovedPrefixes.Should().Contain("sponsorship-types:");
    }

    [Fact]
    public async Task UpdateAsync_can_reactivate_a_disabled_type()
    {
        var entity = TestData.SponsorshipType(6, "Dormant", active: false);
        _repo.GetByIdAsync(6, Arg.Any<CancellationToken>()).Returns(entity);

        var result = await CreateSut().UpdateAsync(6, new UpdateSponsorshipTypeDto("Dormant", IsActive: true));

        result.IsActive.Should().BeTrue();
        entity.IsActive.Should().BeTrue();
    }
}
