namespace QuickerAgent.Core;

/// <summary>
/// getquicker.net ViewTopic official reply (回复主贴 + 提交回复) helpers.
/// page_topic.js addAnswer() reads $('#new-comment').summernote('code') and POSTs /site/CreateAnswer.
/// </summary>
internal static class TopicReplyEditorInterop
{
  public const string NewCommentTextareaId = "new-comment";

  public const string CreateAnswerUrlFragment = "/site/CreateAnswer";

  public const string WriteNewCommentReplyScript = """
    ({ html }) => {
      const jq = window['jQuery'] || window['$'];
      const ta = document.querySelector('#new-comment');
      if (!ta || !jq?.fn?.summernote) {
        return { ok: false, reason: 'no-new-comment-summernote' };
      }

      try {
        if (jq(ta).summernote('codeview.isActivated')) {
          jq(ta).summernote('codeview.toggle');
        }

        jq(ta).summernote('code', html);
        const actual = jq(ta).summernote('code') || '';
        ta.value = actual;
        jq(ta).trigger('input');
        jq(ta).trigger('change');
        jq(ta).trigger('summernote.change');

        const editable = ta.closest('.note-editor')?.querySelector('.note-editing-area .note-editable');
        editable?.dispatchEvent(new InputEvent('input', { bubbles: true }));

        return {
          ok: actual.replace(/<[^>]+>/g, '').trim().length >= 2,
          len: actual.length,
          reason: 'new-comment-api',
        };
      } catch (err) {
        return { ok: false, reason: String(err) };
      }
    }
    """;

  public const string ReadNewCommentReplyScript = """
    () => {
      const jq = window['jQuery'] || window['$'];
      const ta = document.querySelector('#new-comment');
      if (!ta || !jq?.fn?.summernote) {
        return '';
      }

      return jq(ta).summernote('code') || '';
    }
    """;

  public const string HasNewCommentDraftScript = """
    (snippet) => {
      const jq = window['jQuery'] || window['$'];
      const ta = document.querySelector('#new-comment');
      if (!ta || !jq?.fn?.summernote) {
        return false;
      }

      const code = jq(ta).summernote('code') || '';
      const plain = code.startsWith('<')
        ? code.replace(/<[^>]+>/g, ' ')
        : code;
      const needle = (snippet || '').replace(/\\s+/g, '');
      const haystack = plain.replace(/\\s+/g, '');
      return needle.length > 0 && haystack.includes(needle);
    }
    """;
}
