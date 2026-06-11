namespace GestionePrenotazioni.Web.Services.TableAssignment;

public interface ITableAssignmentService
{
    TableAssignmentResult Assign(TableAssignmentRequest request);
}
