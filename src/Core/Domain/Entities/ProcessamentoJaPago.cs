using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class ProcessamentoJaPago
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationUserId { get; set; }

    public int UltimaContabilizacao { get; set; }
}
