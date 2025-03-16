// using Domain.Contracts;
// using Domain.Entities;
// using Domain.Enumerables;
// using Domain.Enums;

// namespace tests;

// public class TimeSelectionEntityTest
// {
//     [Fact]
//     public void Create_ShouldCreateValidTimeSelection()
//     {
//         var perfilId = Guid.NewGuid();
//         var roomId = Guid.NewGuid();
//         var startTime = DateTime.UtcNow;
//         var endTime = DateTime.UtcNow.AddHours(1);
//         var tituloTemporario = "Teste de Título";
//         var tipo = EnumTipoTimeSelection.FreeTime;
//         var tipoAction = TipoAction.Ensinar;
//         var variacao = Variacao.OneToOne;

//         var timeSelection = TimeSelection.Create(
//             perfilId,
//             roomId,
//             startTime,
//             endTime,
//             tituloTemporario,
//             tipo,
//             tipoAction,
//             variacao
//         );

//         Assert.NotNull(timeSelection);
//         Assert.Equal(perfilId, timeSelection.PerfilId);
//         Assert.Equal(roomId, timeSelection.RoomId);
//         Assert.Equal(startTime, timeSelection.StartTime);
//         Assert.Equal(endTime, timeSelection.EndTime);
//         Assert.Equal(tituloTemporario, timeSelection.TituloTemporario);
//         Assert.Equal(tipo, timeSelection.Tipo);
//         Assert.Equal(StatusTimeSelection.Pendente, timeSelection.Status);
//         Assert.False(timeSelection.NotifiedMentoriaProxima);
//         Assert.Equal(tipoAction, timeSelection.TipoAction);
//         Assert.Equal(variacao, timeSelection.Variacao);
//     }

//     [Fact]
//     public void ChangeStatus_ShouldUpdateStatus()
//     {
//         var timeSelection = TimeSelection.Create(
//             Guid.NewGuid(),
//             null,
//             DateTime.UtcNow,
//             DateTime.UtcNow.AddHours(1),
//             null,
//             EnumTipoTimeSelection.FreeTime,
//             TipoAction.Ensinar,
//             Variacao.OneToOne
//         );

//         timeSelection.ChangeStatus(StatusTimeSelection.Concluído);

//         Assert.Equal(StatusTimeSelection.Concluído, timeSelection.Status);
//     }

//     [Fact]
//     public void MarkAsNotified_ShouldSetNotifiedMentoriaProximaToTrue()
//     {
//         var timeSelection = TimeSelection.Create(
//             Guid.NewGuid(),
//             null,
//             DateTime.UtcNow,
//             DateTime.UtcNow.AddHours(1),
//             null,
//             EnumTipoTimeSelection.FreeTime,
//             TipoAction.Ensinar,
//             Variacao.OneToOne
//         );

//         timeSelection.MarkAsNotified();

//         Assert.True(timeSelection.NotifiedMentoriaProxima);
//     }

//     [Fact]
//     public void ChangePreviewImage_ShouldUpdatePreviewImage()
//     {
//         var timeSelection = TimeSelection.Create(
//             Guid.NewGuid(),
//             Guid.NewGuid(),
//             DateTime.UtcNow,
//             DateTime.UtcNow.AddHours(1),
//             "Teste - Change Preview Image",
//             EnumTipoTimeSelection.FreeTime,
//             TipoAction.Ensinar,
//             Variacao.OneToOne
//         );

//         var previewImage = "Nova Preview Image";
//         timeSelection.ChangePreviewImage(previewImage);

//         Assert.Equal(previewImage, timeSelection.PreviewImage);
//     }

//     [Fact]
//     public void ChangeTituloTemporario_ShouldUpdateTituloTemporario()
//     {
//         var timeSelection = TimeSelection.Create(
//             Guid.NewGuid(),
//             Guid.NewGuid(),
//             DateTime.UtcNow,
//             DateTime.UtcNow.AddHours(1),
//             "Teste - Change Preview Image",
//             EnumTipoTimeSelection.FreeTime,
//             TipoAction.Ensinar,
//             Variacao.OneToOne
//         );

//         var novoTituloTemporario = "Novo Título Temporário";
//         timeSelection.ChangeTituloTemporario(novoTituloTemporario);

//         Assert.Equal(novoTituloTemporario, timeSelection.TituloTemporario);
//     }
// }
