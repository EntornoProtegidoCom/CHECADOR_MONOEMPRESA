using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChecadorWeb.Pages.Account;

public class AccesoDenegadoModel : PageModel
{
    public void OnGet() => Response.StatusCode = StatusCodes.Status403Forbidden;
}
