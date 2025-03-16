// using Domain.Contracts;
// using Domain.Entities;
// using Domain.Enumerables;
// using Domain.Enums;
// using Domain.Repositories;
// using Infrastructure.Contexts;
// using Infrastructure.Repositories;
// using Microsoft.EntityFrameworkCore;

// namespace tests;

// public class TimeSelectionRepositoryTests
// {
//     private readonly TimeSelectionRepository _repository;
//     private readonly ApplicationDbContext _context;

//     public TimeSelectionRepositoryTests()
//     {
//         var options = new DbContextOptionsBuilder<ApplicationDbContext>()
//             .UseInMemoryDatabase(databaseName: "TestDatabase")
//             .Options;

//         _context = new ApplicationDbContext(options);
//         _repository = new TimeSelectionRepository(_context);
//     }

//     private void InitializeTimeSelections()
//     {
//         _context.TimeSelections.AddRange(
//             new List<TimeSelection>
//             {
//                 TimeSelection.Create(
//                     Guid.NewGuid(),
//                     null,
//                     DateTime.Now.AddMinutes(-20),
//                     DateTime.Now.AddMinutes(20),
//                     "Teste de descrição",
//                     EnumTipoTimeSelection.FreeTime,
//                     TipoAction.Ensinar,
//                     Variacao.OneToOne
//                 ),
//                 TimeSelection.Create(
//                     Guid.NewGuid(),
//                     null,
//                     DateTime.Now.AddMinutes(-20),
//                     DateTime.Now.AddMinutes(20),
//                     "Teste de descrição",
//                     EnumTipoTimeSelection.FreeTime,
//                     TipoAction.Ensinar,
//                     Variacao.OneToOne
//                 ),
//             }
//         );
//         _context.SaveChanges();
//     }

//     [Fact]
//     public async Task GetById_ReturnsTimeSelection()
//     {
//         InitializeTimeSelections();

//         var expected = _context.TimeSelections.First();

//         var result = await _repository.GetById(expected.Id);

//         Assert.NotNull(result);
//         Assert.Equal(expected.Id, result.Id);
//     }

//     [Fact]
//     public async Task GetFreeTimeMarcadosAntigos_ReturnsTimeSelections()
//     {
//         var result = await _repository.GetFreeTimeMarcadosAntigos();

//         Assert.NotNull(result);
//         Assert.All(
//             result,
//             ts =>
//                 Assert.True(
//                     ts.Tipo == EnumTipoTimeSelection.FreeTime
//                         && ts.Status == StatusTimeSelection.Marcado
//                         && ts.EndTime < DateTime.Now
//                 )
//         );
//     }

//     [Fact]
//     public async Task GetTimeSelectionPerfilIdsByJoinTimeIds_ReturnsDictionary()
//     {
//         var joinTimeIds = _context.JoinTimes.Select(jt => jt.Id).ToList();

//         var result = await _repository.GetTimeSelectionPerfilIdsByJoinTimeIds(joinTimeIds);

//         Assert.NotNull(result);
//         Assert.All(
//             result.Keys,
//             key => Assert.Contains(_context.TimeSelections, ts => ts.Id == key)
//         );
//     }

//     [Fact]
//     public async Task UpdateRange_UpdatesTimeSelections()
//     {
//         InitializeTimeSelections();
//         var timeSelectionsToUpdate = _context.TimeSelections.Take(2).ToList();
//         foreach (var ts in timeSelectionsToUpdate)
//         {
//             ts.ChangeStatus(StatusTimeSelection.Concluído);
//         }

//         await _repository.UpdateRange(timeSelectionsToUpdate);

//         var updatedTimeSelections = timeSelectionsToUpdate
//             .Select(ts => _context.TimeSelections.Find(ts.Id))
//             .ToList();
//         Assert.All(
//             updatedTimeSelections,
//             ts => Assert.Equal(StatusTimeSelection.Concluído, ts?.Status)
//         );
//     }

//     [Fact]
//     public async Task GetUpcomingTimeSelectionAndJoinTime_ReturnsDictionary()
//     {
//         var result = await _repository.GetUpcomingTimeSelectionAndJoinTime();

//         Assert.NotNull(result);
//         Assert.All(
//             result.Keys,
//             ts =>
//                 Assert.True(
//                     ts.Tipo == EnumTipoTimeSelection.FreeTime
//                         && ts.Status == StatusTimeSelection.Marcado
//                         && !ts.NotifiedMentoriaProxima
//                         && ts.StartTime > DateTime.Now.AddMinutes(-30)
//                 )
//         );
//     }
// }
