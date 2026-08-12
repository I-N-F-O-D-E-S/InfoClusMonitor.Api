using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Transfers;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransfersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FileTransferDto>>> GetAll([FromQuery] string? machineId = null)
    {
        var transfers = await mediator.Send(new GetAllTransfersQuery(machineId));
        return Ok(transfers);
    }

    [HttpGet("{transferId}")]
    public async Task<ActionResult<FileTransferDto>> GetById(string transferId)
    {
        var transfer = await mediator.Send(new GetTransferByIdQuery(transferId));
        if (transfer == null)
            return NotFound();

        return Ok(transfer);
    }

    [HttpPost]
    public async Task<ActionResult<FileTransferDto>> StartTransfer([FromBody] StartTransferDto req)
    {
        if (string.IsNullOrWhiteSpace(req.SourceMachineId) || string.IsNullOrWhiteSpace(req.SourcePath))
            return BadRequest("El servidor de origen y la ruta de origen son obligatorios.");

        if (string.IsNullOrWhiteSpace(req.TargetMachineId) || string.IsNullOrWhiteSpace(req.TargetPath))
            return BadRequest("El servidor de destino y la ruta de destino son obligatorios.");

        try
        {
            var entity = await mediator.Send(new CreateTransferCommand(req));
            var transfer = await mediator.Send(new GetTransferByIdQuery(entity.TransferId));
            return Ok(transfer);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{transferId}")]
    public async Task<ActionResult> CancelTransfer(string transferId)
    {
        var result = await mediator.Send(new CancelTransferCommand(transferId));
        if (!result)
            return NotFound();

        return NoContent();
    }
}
