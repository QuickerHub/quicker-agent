namespace QuickerAgent.Core;

/// <summary>
/// Summernote / getquicker.net edit-page DOM helpers (source view read/write).
/// </summary>
internal static class ActionDescriptionEditorInterop
{
  public static string EnsureSourceViewScript => $$"""
    () => {
      const editor = document.querySelector('.note-editor.note-frame');
      if (editor && editor.classList.contains('codeview')) {
        return true;
      }
      const jq = window['jQuery'] || window['$'];
      const sn = {{GetQuickerActionDocPage.SummernoteRootJs}};
      if (jq && sn && jq.fn && jq.fn.summernote) {
        if (!jq(sn).summernote('codeview.isActivated')) {
          jq(sn).summernote('codeview.toggle');
        }
        const ed = document.querySelector('.note-editor.codeview');
        return !!ed;
      }
      const btn = Array.from(document.querySelectorAll('button')).find(
        b => (b.getAttribute('aria-label') || b.textContent || '').includes('源代码'));
      if (btn) {
        btn.click();
      }
      return !!document.querySelector('.note-editor.codeview, .note-codable');
    }
    """;

  public const string DispatchSourceInputScript = """
    () => {
      const selectors = [
        'body > div.body-wrapper > div > form > div > div.col-md-9 > div:nth-child(5) > div > div.note-editor.note-frame.card.codeview > div.note-editing-area > textarea',
        'div.note-editor.codeview .note-editing-area textarea',
        '.note-codable',
      ];
      for (const sel of selectors) {
        const ta = document.querySelector(sel);
        if (ta) {
          ta.dispatchEvent(new Event('input', { bubbles: true }));
          ta.dispatchEvent(new Event('change', { bubbles: true }));
          return sel;
        }
      }
      return null;
    }
    """;

  /// <summary>Fallback when toolbar click fails: toggle codeview off so textarea content applies to preview.</summary>
  public static string ExitSourceViewScript => $$"""
    () => {
      const jq = window['jQuery'] || window['$'];
      const sn = {{GetQuickerActionDocPage.SummernoteRootJs}};
      if (jq && sn && jq.fn && jq.fn.summernote && jq(sn).summernote('codeview.isActivated')) {
        jq(sn).summernote('codeview.toggle');
      }
      const editable = document.querySelector('.note-editable');
      return editable && editable.offsetParent !== null ? 'preview' : 'toggled';
    }
    """;

  public static string ReadSummernoteCodeScript => $$"""
    () => {
      const jq = window['jQuery'] || window['$'];
      const sn = {{GetQuickerActionDocPage.SummernoteRootJs}};
      if (jq && sn && jq.fn && jq.fn.summernote) {
        return jq(sn).summernote('code');
      }
      const ed = document.querySelector('.note-editable');
      if (ed) {
        return ed.innerHTML;
      }
      return null;
    }
    """;
}
