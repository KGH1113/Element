/** How a question is asked before something is done. */
export interface ConfirmOptions {
  /** The word on the button that goes ahead: the action itself ("Delete"). */
  confirmText?: string;
  cancelText?: string;
  /**
   * Going ahead cannot be taken back (deleting, resetting). The button is
   * drawn as such, and the dialog opens on Cancel so Enter changes nothing.
   */
  danger?: boolean;
  onCancel?: () => void;
}

export type ShowConfirm = (
  message: string,
  onConfirm: () => void,
  options?: ConfirmOptions,
) => void;
