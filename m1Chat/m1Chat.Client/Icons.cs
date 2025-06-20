using MudBlazor;


namespace m1Chat.Client;

// Extend MudBlazor.Icons with custom icons
public static class CustomIcons
{
    public static class Brands
    {
        // Google icon SVG path
        public const string Google = "M22.000 12.164c0-.756-.067-1.474-.192-2.168H12v4.582h6.14c-.267 1.48-.962 2.73-2.035 3.593v3.018h2.985c1.748-1.614 2.75-3.993 2.75-6.536zM12 22c3.235 0 5.962-1.07 7.95-2.915l-2.986-3.018c-.8.536-1.83 1.07-2.964 1.07-2.28 0-4.22-1.536-4.92-3.613H4.07V15.01c1.378 2.658 4.093 4.54 7.93 4.54zm-7.93-9.52c-.22-.64-.343-1.32-.343-2s.123-1.36.343-2v-3.49H4.07V9.01C2.793 10.658 2 12.72 2 15s.793 4.342 2.07 5.99v-3.49h3.18V12.48zM12 6.54c1.777 0 3.355.617 4.59 1.764l2.67-2.67C17.96 4.417 15.235 3 12 3c-3.837 0-6.552 1.882-7.93 4.54l3.18 2.49c.7-2.077 2.64-3.613 4.92-3.613z";
    }
}


public class SvgIcons
{
    public readonly string[] BrainIcons = [BrainNone, BrainLow, BrainMedium, BrainHigh];

    // Added the Google icon from CustomIcons.Brands
    public readonly string Google = CustomIcons.Brands.Google;

    private const string BrainNone = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-brain h-4 w-4"><path d="M12,4.69c0-1.66,1.33-3,2.99-3.01,1.18,0,1.94,.82,2.65,1.65,.94,1.09,1.84,2.27,2.51,3.55,.82,1.55,1.08,3.17,1.27,4.89,.1,.94,.12,1.9-.02,2.84s-.5,1.7-.92,2.56c-.45,.94-.7,2.03-1.26,2.89-.57,.88-1.68,1.46-2.7,1.59-2.19,.28-4.2-1.26-4.48-3.45-.02-.17-.03-.34-.03-.51V4.69Z"></path><path d="M12,17.69c0,.17-.01,.34-.03,.51-.28,2.19-2.29,3.74-4.48,3.45-1.02-.13-2.13-.71-2.7-1.59-.56-.86-.81-1.96-1.26-2.89-.42-.87-.78-1.59-.92-2.56s-.12-1.9-.02-2.84c.19-1.72,.45-3.34,1.27-4.89,.67-1.28,1.57-2.46,2.51-3.55,.71-.83,1.47-1.66,2.65-1.65,1.66,0,3,1.35,2.99,3.01v13Z"></path><path d="M13.32,12.24c-.76-.27-1.28-.96-1.32-1.76-.04,.8-.57,1.5-1.32,1.76"></path><line x1="4" y1="4" x2="20" y2="20" stroke="currentColor" stroke-width="2" stroke-linecap="round"></line></svg>""";
    private const string BrainLow = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-brain h-4 w-4"><path d="M12,4.69c0-1.66,1.33-3,2.99-3.01,1.18,0,1.94,.82,2.65,1.65,.94,1.09,1.84,2.27,2.51,3.55,.82,1.55,1.08,3.17,1.27,4.89,.1,.94,.12,1.9-.02,2.84s-.5,1.7-.92,2.56c-.45,.94-.7,2.03-1.26,2.89-.57,.88-1.68,1.46-2.7,1.59-2.19,.28-4.2-1.26-4.48-3.45-.02-.17-.03-.34-.03-.51V4.69Z"></path><path d="M12,17.69c0,.17-.01,.34-.03,.51-.28,2.19-2.29,3.74-4.48,3.45-1.02-.13-2.13-.71-2.7-1.59-.56-.86-.81-1.96-1.26-2.89-.42-.87-.78-1.59-.92-2.56s-.12-1.9-.02-2.84c.19-1.72,.45-3.34,1.27-4.89,.67-1.28,1.57-2.46,2.51-3.55,.71-.83,1.47-1.66,2.65-1.65,1.66,0,3,1.35,2.99,3.01v13Z"></path><path d="M13.32,12.24c-.76-.27-1.28-.96-1.32-1.76-.04,.8-.57,1.5-1.32,1.76"></path></svg>""";
    private const string BrainMedium = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-brain size-4"><path d="M12 5a3 3 0 1 0-5.997.125 4 4 0 0 0-2.526 5.77 4 4 0 0 0 .556 6.588A4 4 0 1 0 12 18Z"></path><path d="M12 5a3 3 0 1 1 5.997.125 4 4 0 0 1 2.526 5.77 4 4 0 0 1-.556 6.588A4 4 0 1 1 12 18Z"></path><path d="M15 13a4.5 4.5 0 0 1-3-4 4.5 4.5 0 0 1-3 4"></path><path d="M17.599 6.5a3 3 0 0 0 .399-1.375"></path><path d="M6.003 5.125A3 3 0 0 0 6.401 6.5"></path><path d="M3.477 10.896a4 4 0 0 1 .585-.396"></path><path d="M19.938 10.5a4 4 0 0 1 .585.396"></path><path d="M6 18a4 4 0 0 1-1.967-.516"></path><path d="M19.967 17.484A4 4 0 0 1 18 18"></path></svg>""";
    private const string BrainHigh = """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-brain h-4 w-4"><path d="M12,4.69c0-1.66-1.33-3-2.99-3.01-1.66,0-3,1.33-3.01,2.99,0,.05,0,.1,0,.14-2.14,.55-3.43,2.73-2.88,4.87,.08,.31,.2,.62,.35,.9-1.71,1.39-1.98,3.91-.58,5.63,.32,.39,.7,.71,1.14,.96-.28,2.19,1.26,4.2,3.45,4.48,2.19,.28,4.2-1.26-4.48-3.45-.02-.17-.03-.34-.03-.51V4.69Z"></path><path d="M6,4.81c.02,.48,.32,2.19,2.42,2.76"></path><path d="M3.48,10.58c.93-.61,1.8-.83,2.88-.7"></path><path d="M7.85,17.43c-1.49,.47-3.22,.08-3.82-.26"></path><path d="M12,17.69c0,.17,.01,.34,.03,.51,.28,2.19,2.29,3.74,4.48,3.45,2.19-.28,3.74-2.29,3.45-4.48,.44-.25,.82-.57,1.14-.96,1.39-1.71,1.13-4.23-.58-5.63,.15-.28,.27-.59,.35-.9,.55-2.14-.74-4.32-2.88-4.87,0-.05,0-.1,0-.14,0-1.66-1.35-3-3.01-2.99-1.66,0-3,1.35-2.99,3.01v13Z"></path><path d="M15.58,7.57c2.1-.57,2.4-2.28,2.42-2.76"></path><path d="M17.64,9.88c1.08-.13,1.95,.1,2.88,.7"></path><path d="M19.97,17.17c-.6,.34-2.33,.73-3.82,.26"></path><path d="M17.22,13.44c-3.72,1.79-5.31-1.79-5.21-4.66"></path><path d="M11.94,8.78c.1,2.87-1.49,6.45-5.21,4.66"></path></svg>""";

    public const string SideBar =
        """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-panel-left"><rect width="18" height="18" x="3" y="3" rx="2"></rect><path d="M9 3v18"></path></svg>""";

    public const string New =
        """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-plus"><path d="M5 12h14"></path><path d="M12 5v14"></path></svg>""";

    public const string Search =
        """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-search"><circle cx="11" cy="11" r="8"></circle><path d="m21 21-4.3-4.3"></path></svg>""";
}
