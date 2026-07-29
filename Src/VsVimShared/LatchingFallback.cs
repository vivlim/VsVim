using System;

namespace Vim.VisualStudio;

/// <summary>
/// Stateful container for running one of two actions depending on whether the first one throws or not.
/// </summary>
internal class LatchingFallback(Action action, Action fallbackAction)
{
    private bool? _fallback = null;

    public void Execute()
    {
        if (_fallback == false)
        {
            action();
            return;
        }
        else if (_fallback == true)
        {
            fallbackAction();
            return;
        }

        try
        {
            action();
            _fallback = false; // it worked
            return;
        }
        catch (Exception)
        {
            try
            {
                fallbackAction();
                _fallback = true; // it didn't work but the fallback did, so let's keep using that
                return;
            }
            catch
            {
                // neither worked. just settle on one of them instead of continuing to try both
                _fallback = false;
            }
        }
    }


}
