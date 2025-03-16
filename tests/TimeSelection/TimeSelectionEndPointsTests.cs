// using Application.Logic;
// using Domain.Entities;
// using Domain.Enums;
// using Microsoft.AspNetCore.Http.HttpResults;
// using Microsoft.AspNetCore.Mvc;
// using Moq;
// using Presentation.EndPoints;

// namespace tests;

// public class TimeSelectionEndPointsTests
// {
//     [Fact]
//     public async Task GetById_ReturnsOkResult_WithTimeSelection()
//     {
//         // Mock do ITimeSelectionBusinessLogic
//         var mockLogic = new Mock<ITimeSelectionBusinessLogic>();
//         var testTimeSelection = TimeSelection.Create(
//             Guid.NewGuid(),
//             null,
//             DateTime.Now,
//             DateTime.Now.AddHours(1),
//             "Guid.NewGuid()",
//             EnumTipoTimeSelection.FreeTime,
//             TipoAction.Ensinar,
//             Variacao.OneToOne
//         );
//         var testId = testTimeSelection.Id;

//         mockLogic.Setup(logic => logic.GetById(testId)).ReturnsAsync(testTimeSelection);

//         // Chama o método GetById e verifica o resultado
//         var result = await TimeSelectionEndPoints.GetById(mockLogic.Object, testId);

//         var response = Assert.IsType<Ok<TimeSelection>>(result);
//         Assert.Equal(testTimeSelection, response.Value);
//     }

//     [Fact]
//     public async Task GetById_ReturnsBadRequest_OnException()
//     {
//         // Mock do ITimeSelectionBusinessLogic
//         var mockLogic = new Mock<ITimeSelectionBusinessLogic>();
//         var testId = Guid.NewGuid();

//         mockLogic.Setup(logic => logic.GetById(testId)).ThrowsAsync(new Exception("Erro de teste"));

//         // Chama o método GetById e verifica o resultado
//         var result = await TimeSelectionEndPoints.GetById(mockLogic.Object, testId);

//         Assert.IsType<BadRequest<string>>(result);
//     }

//     [Fact]
//     public async Task UpdateOldTimeSelections_ReturnsOkResult()
//     {
//         // Mock do ITimeSelectionBusinessLogic
//         var mockLogic = new Mock<ITimeSelectionBusinessLogic>();

//         mockLogic.Setup(logic => logic.UpdateOldTimeSelections()).Returns(Task.CompletedTask);

//         // Chama o método UpdateOldTimeSelections e verifica o resultado
//         var result = await TimeSelectionEndPoints.UpdateOldTimeSelections(mockLogic.Object);

//         Assert.IsType<Ok>(result);
//     }

//     [Fact]
//     public async Task UpdateOldTimeSelections_ReturnsBadRequest_OnException()
//     {
//         // Mock do ITimeSelectionBusinessLogic
//         var mockLogic = new Mock<ITimeSelectionBusinessLogic>();

//         mockLogic
//             .Setup(logic => logic.UpdateOldTimeSelections())
//             .ThrowsAsync(new Exception("Erro de teste"));

//         // Chama o método UpdateOldTimeSelections e verifica o resultado
//         var result = await TimeSelectionEndPoints.UpdateOldTimeSelections(mockLogic.Object);

//         Assert.IsType<BadRequest<string>>(result);
//     }

//     [Fact]
//     public async Task NotifyUpcomingTimeSelectionAndJoinTime_ReturnsOkResult()
//     {
//         // Mock do ITimeSelectionBusinessLogic
//         var mockLogic = new Mock<ITimeSelectionBusinessLogic>();

//         mockLogic
//             .Setup(logic => logic.NotifyUpcomingTimeSelectionAndJoinTime())
//             .Returns(Task.CompletedTask);

//         // Chama o método NotifyUpcomingTimeSelectionAndJoinTime e verifica o resultado
//         var result = await TimeSelectionEndPoints.NotifyUpcomingTimeSelectionAndJoinTime(
//             mockLogic.Object
//         );

//         Assert.IsType<Ok>(result);
//     }

//     [Fact]
//     public async Task NotifyUpcomingTimeSelectionAndJoinTime_ReturnsBadRequest_OnException()
//     {
//         // Mock do ITimeSelectionBusinessLogic
//         var mockLogic = new Mock<ITimeSelectionBusinessLogic>();

//         mockLogic
//             .Setup(logic => logic.NotifyUpcomingTimeSelectionAndJoinTime())
//             .ThrowsAsync(new Exception("Erro de teste"));

//         // Chama o método NotifyUpcomingTimeSelectionAndJoinTime e verifica o resultado
//         var result = await TimeSelectionEndPoints.NotifyUpcomingTimeSelectionAndJoinTime(
//             mockLogic.Object
//         );

//         Assert.IsType<BadRequest<string>>(result);
//     }
// }
