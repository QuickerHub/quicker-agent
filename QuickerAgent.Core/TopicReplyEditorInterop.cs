namespace QuickerAgent.Core;

/// <summary>
/// getquicker.net ViewTopic official reply (回复主贴 + 提交回复) Summernote helpers.
/// </summary>
internal static class TopicReplyEditorInterop
{
  /// <summary>Locate the first Summernote editor after the 回复主贴 heading.</summary>
  public const string FindOfficialReplyEditorIndexScript = """
    () => {
      const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
      if (!h5) {
        return -1;
      }

      const allEditors = [...document.querySelectorAll('.note-editor')];
      let node = h5;
      while (node) {
        node = node.nextElementSibling;
        if (!node) break;
        const editor = node.classList?.contains('note-editor')
          ? node
          : node.querySelector?.('.note-editor');
        if (editor) {
          const index = allEditors.indexOf(editor);
          if (index >= 0) return index;
        }
      }

      for (let i = 0; i < allEditors.length; i++) {
        if (h5.compareDocumentPosition(allEditors[i]) & Node.DOCUMENT_POSITION_FOLLOWING) {
          return i;
        }
      }

      return -1;
    }
    """;

  public const string FindOfficialReplyTextareaScript = """
    () => {
      const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
      if (!h5) {
        return null;
      }

      for (const editor of document.querySelectorAll('.note-editor')) {
        if (h5.compareDocumentPosition(editor) & Node.DOCUMENT_POSITION_FOLLOWING) {
          const ta = editor.querySelector('textarea');
          if (ta) {
            return ta;
          }
        }
      }

      return null;
    }
    """;

  public const string WriteOfficialReplyScript = """
    ({ html }) => {
      const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
      if (!h5) {
        return { ok: false, reason: 'no-heading' };
      }

      let editor = null;
      for (const node of document.querySelectorAll('.note-editor')) {
        if (h5.compareDocumentPosition(node) & Node.DOCUMENT_POSITION_FOLLOWING) {
          editor = node;
          break;
        }
      }

      if (!editor) {
        return { ok: false, reason: 'no-editor' };
      }

      const ta = editor.querySelector('textarea');
      const jq = window['jQuery'] || window['$'];
      if (!ta || !jq?.fn?.summernote) {
        return { ok: false, reason: 'no-summernote' };
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

        const editable = editor.querySelector('.note-editable');
        if (editable) {
          editable.dispatchEvent(new InputEvent('input', { bubbles: true }));
          editable.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true }));
        }

        const btn = [...document.querySelectorAll('button')].find(b => (b.textContent || '').includes('提交回复'));
        return {
          ok: actual.length >= Math.min(html.length, 32),
          len: actual.length,
          taValueLen: (ta.value || '').length,
          submitEnabled: !!btn && !btn.disabled,
          reason: 'api',
        };
      } catch (err) {
        return { ok: false, reason: String(err) };
      }
    }
    """;

  public const string WriteOfficialReplyViaSourceViewScript = """
    ({ html }) => {
      const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
      if (!h5) {
        return { ok: false, reason: 'no-heading' };
      }

      let editor = null;
      for (const node of document.querySelectorAll('.note-editor')) {
        if (h5.compareDocumentPosition(node) & Node.DOCUMENT_POSITION_FOLLOWING) {
          editor = node;
          break;
        }
      }

      if (!editor) {
        return { ok: false, reason: 'no-editor' };
      }

      const jq = window['jQuery'] || window['$'];
      const ta = editor.querySelector('textarea');
      if (!ta || !jq?.fn?.summernote) {
        return { ok: false, reason: 'no-summernote' };
      }

      try {
        if (!jq(ta).summernote('codeview.isActivated')) {
          const sourceBtn = [...editor.querySelectorAll('button')].find(b => {
            const label = b.getAttribute('aria-label') || b.getAttribute('data-original-title') || b.textContent || '';
            return label.includes('源代码') || label.toLowerCase().includes('code view');
          });
          if (sourceBtn) {
            sourceBtn.click();
          } else {
            jq(ta).summernote('codeview.toggle');
          }
        }

        const codable = editor.querySelector('.note-codable')
          || editor.querySelector('.note-editor.codeview textarea')
          || editor.querySelector('textarea');
        if (!codable) {
          return { ok: false, reason: 'no-codable' };
        }

        codable.value = html;
        codable.dispatchEvent(new Event('input', { bubbles: true }));
        codable.dispatchEvent(new Event('change', { bubbles: true }));

        if (jq(ta).summernote('codeview.isActivated')) {
          const sourceBtn = [...editor.querySelectorAll('button')].find(b => {
            const label = b.getAttribute('aria-label') || b.getAttribute('data-original-title') || b.textContent || '';
            return label.includes('源代码') || label.toLowerCase().includes('code view');
          });
          if (sourceBtn) {
            sourceBtn.click();
          } else {
            jq(ta).summernote('codeview.toggle');
          }
        }

        const actual = jq(ta).summernote('code') || '';
        ta.value = actual;
        jq(ta).trigger('input');
        jq(ta).trigger('change');
        jq(ta).trigger('summernote.change');

        const btn = [...document.querySelectorAll('button')].find(b => (b.textContent || '').includes('提交回复'));
        return {
          ok: actual.length >= Math.min(html.length, 32),
          len: actual.length,
          submitEnabled: !!btn && !btn.disabled,
          reason: 'source',
        };
      } catch (err) {
        return { ok: false, reason: String(err) };
      }
    }
    """;

  public const string ReadOfficialReplyCodeScript = """
    () => {
      const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
      if (!h5) {
        return '';
      }

      for (const editor of document.querySelectorAll('.note-editor')) {
        if (!(h5.compareDocumentPosition(editor) & Node.DOCUMENT_POSITION_FOLLOWING)) {
          continue;
        }

        const jq = window['jQuery'] || window['$'];
        const ta = editor.querySelector('textarea');
        if (ta && jq?.fn?.summernote) {
          return jq(ta).summernote('code') || '';
        }

        return editor.querySelector('.note-editable')?.innerHTML || '';
      }

      return '';
    }
    """;

  public const string SyncOfficialReplyFromEditableScript = """
    (index) => {
      const editor = document.querySelectorAll('.note-editor')[index];
      if (!editor) return { ok: false, reason: 'no-editor' };
      const editable = editor.querySelector('.note-editing-area .note-editable');
      const ta = editor.querySelector('textarea');
      const jq = window['jQuery'] || window['$'];
      if (!editable || !ta || !jq?.fn?.summernote) return { ok: false, reason: 'no-summernote' };
      const plain = (editable.innerText || '').trim();
      if (!plain) return { ok: false, reason: 'empty-editable' };
      const html = plain.split(/\\n\\n+/).map(p => '<p>' + p.replace(/\\n/g, '<br>') + '</p>').join('');
      jq(ta).summernote('code', html);
      ta.value = jq(ta).summernote('code') || html;
      jq(ta).trigger('input');
      jq(ta).trigger('change');
      jq(ta).trigger('summernote.change');
      editable.dispatchEvent(new InputEvent('input', { bubbles: true }));
      const btn = [...document.querySelectorAll('button')].find(b => (b.textContent || '').includes('提交回复'));
      return {
        ok: true,
        len: plain.length,
        submitEnabled: !!btn && !btn.disabled,
        reason: 'sync-from-editable',
      };
    }
    """;

  public const string SubmitOfficialReplyScript = """
    () => {
      const btn = [...document.querySelectorAll('button')].find(b => (b.textContent || '').includes('提交回复'));
      if (!btn) {
        return { ok: false, reason: 'no-button' };
      }

      const vueParent = btn.__vueParentComponent;
      const vue2 = btn.__vue__;
      if (vueParent?.ctx?.submitReply) {
        vueParent.ctx.submitReply();
        return { ok: true, reason: 'vue3-submitReply' };
      }
      if (vueParent?.ctx?.onSubmitReply) {
        vueParent.ctx.onSubmitReply();
        return { ok: true, reason: 'vue3-onSubmitReply' };
      }
      if (vue2?.$parent?.submitReply) {
        vue2.$parent.submitReply();
        return { ok: true, reason: 'vue2-parent-submitReply' };
      }

      btn.click();
      return { ok: true, reason: 'click' };
    }
    """;

  public const string IsSubmitReplyEnabledScript = """
    () => {
      const btn = [...document.querySelectorAll('button')].find(b => (b.textContent || '').includes('提交回复'));
      return !!btn && !btn.disabled;
    }
    """;
}
