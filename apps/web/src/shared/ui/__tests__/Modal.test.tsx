import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { Modal } from '../Modal';

function ModalTestWrapper() {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div>
      <button type="button" id="trigger-btn" onClick={() => setIsOpen(true)}>
        Open Modal
      </button>

      <Modal
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        title="Test Modal"
        description="Test modal description"
      >
        <div>
          <button type="button" id="modal-first-btn">
            First Action
          </button>
          <button type="button" id="modal-second-btn">
            Second Action
          </button>
        </div>
      </Modal>
    </div>
  );
}

describe('Modal', () => {
  it('does not render dialog when isOpen is false', () => {
    render(
      <Modal isOpen={false} onClose={() => {}} title="Hidden Modal">
        Content
      </Modal>
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders with role dialog, aria-modal=true, and labelledby title when isOpen is true', () => {
    render(
      <Modal
        isOpen={true}
        onClose={() => {}}
        title="Settings Modal"
        description="Configuration description"
      >
        Content
      </Modal>
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    expect(dialog).toHaveAttribute('aria-modal', 'true');

    const heading = screen.getByRole('heading', { name: 'Settings Modal' });
    expect(dialog).toHaveAttribute('aria-labelledby', heading.id);
  });

  it('calls onClose when close button is clicked', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();

    render(
      <Modal isOpen={true} onClose={handleClose} title="Modal Title">
        Content
      </Modal>
    );

    const closeButton = screen.getByRole('button', { name: 'Close modal' });
    await user.click(closeButton);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when Escape key is pressed', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();

    render(
      <Modal isOpen={true} onClose={handleClose} title="Escape Test">
        <button type="button">Inside</button>
      </Modal>
    );

    await user.keyboard('{Escape}');
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when clicking outside overlay', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();

    render(
      <Modal isOpen={true} onClose={handleClose} title="Backdrop Test">
        Content
      </Modal>
    );

    const backdrop = screen.getByTestId('modal-backdrop');
    await user.click(backdrop);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('restores focus to trigger element when closed', async () => {
    const user = userEvent.setup();
    render(<ModalTestWrapper />);

    const triggerBtn = screen.getByRole('button', { name: 'Open Modal' });
    await user.click(triggerBtn);

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toBeInTheDocument();

    const closeBtn = screen.getByRole('button', { name: 'Close modal' });
    await user.click(closeBtn);

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(triggerBtn).toHaveFocus();
    });
  });

  it('traps focus within modal during keyboard Tab navigation', async () => {
    const user = userEvent.setup();
    render(
      <Modal isOpen={true} onClose={() => {}} title="Trap Test">
        <button type="button" id="btn-1">
          First
        </button>
        <button type="button" id="btn-2">
          Second
        </button>
      </Modal>
    );

    const closeBtn = screen.getByRole('button', { name: 'Close modal' });
    const btn1 = screen.getByRole('button', { name: 'First' });
    const btn2 = screen.getByRole('button', { name: 'Second' });

    // Focus on close button (first or close button)
    closeBtn.focus();
    expect(closeBtn).toHaveFocus();

    await user.tab();
    expect(btn1).toHaveFocus();

    await user.tab();
    expect(btn2).toHaveFocus();

    // Tabbing from last element wraps to first element
    await user.tab();
    expect(closeBtn).toHaveFocus();

    // Shift-tab from first element wraps to last element
    await user.tab({ shift: true });
    expect(btn2).toHaveFocus();
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <Modal isOpen={true} onClose={() => {}} title="Accessible Modal">
        <p>Modal content body</p>
        <button type="button">Action</button>
      </Modal>
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
