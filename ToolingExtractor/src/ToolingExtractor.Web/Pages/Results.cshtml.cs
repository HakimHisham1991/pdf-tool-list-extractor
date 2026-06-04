using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ToolingExtractor.Web.Pages;

public class ResultsModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/FilesProcessed");
}
