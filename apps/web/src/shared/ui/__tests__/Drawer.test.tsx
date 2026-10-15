import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { Drawer } from '../Drawer';

function DrawerTestWrapper() {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div>
      <button
        type="button"
        id="drawer-trigger-btn"
        onClick={() => setIsOpen(true)}
      >
        Open Drawer
      </button>

      <Drawer
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        title="Audit Panel"
        description="Audit log drawer"
      >
        <div>
          <button type="button" id="drawer-action-1">
            Action 1
          </button>
          <button type="button" id="drawer-action-2">
            Action 2
          </button>
        </div>
      </Drawer>
    </div>
  );
}

describe('Drawer', () => {
  it('does not render dialog when isOpen is false', () => {
    render(
      <Drawer isOpen={false} onClose={() => {}} title="Hidden Drawer">
        Content
      </Drawer>
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders with role dialog, aria-modal=true, and labelledby title when isOpen is true', () => {
    render(
      <Drawer
        isOpen={true}
        onClose={() => {}}
        title="Settings Drawer"
        description="Panel description"
      >
        Content
      </Drawer>
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    expect(dialog).toHaveAttribute('aria-modal', 'true');

    const heading = screen.getByRole('heading', { name: 'Settings Drawer' });
    expect(dialog).toHaveAttribute('aria-labelledby', heading.id);
  });

  it('calls onClose when close button is clicked', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();

    render(
      <Drawer isOpen={true} onClose={handleClose} title="Drawer Title">
        Content
      </Drawer>
    );

    const closeButton = screen.getByRole('button', { name: 'Close drawer' });
    await user.click(closeButton);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when Escape key is pressed', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();

    render(
      <Drawer isOpen={true} onClose={handleClose} title="Escape Drawer">
        <button type="button">Inside</button>
      </Drawer>
    );

    await user.keyboard('{Escape}');
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when clicking backdrop overlay', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();

    render(
      <Drawer isOpen={true} onClose={handleClose} title="Backdrop Drawer">
        Content
      </Drawer>
    );

    const backdrop = screen.getByTestId('drawer-backdrop');
    await user.click(backdrop);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it('restores focus to trigger element when closed', async () => {
    const user = userEvent.setup();
    render(<DrawerTestWrapper />);

    const triggerBtn = screen.getByRole('button', { name: 'Open Drawer' });
    await user.click(triggerBtn);

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toBeInTheDocument();

    const closeBtn = screen.getByRole('button', { name: 'Close drawer' });
    await user.click(closeBtn);

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(triggerBtn).toHaveFocus();
    });
  });

  it('traps focus within drawer during keyboard Tab navigation', async () => {
    const user = userEvent.setup();
    render(
      <Drawer isOpen={true} onClose={() => {}} title="Trap Drawer">
        <button type="button" id="d-btn-1">
          First
        </button>
        <button type="button" id="d-btn-2">
          Second
        </button>
      </Drawer>
    );

    const closeBtn = screen.getByRole('button', { name: 'Close drawer' });
    const btn1 = screen.getByRole('button', { name: 'First' });
    const btn2 = screen.getByRole('button', { name: 'Second' });

    closeBtn.focus();
    expect(closeBtn).toHaveFocus();

    await user.tab();
    expect(btn1).toHaveFocus();

    await user.tab();
    expect(btn2).toHaveFocus();

    // Wrap around to first element
    await user.tab();
    expect(closeBtn).toHaveFocus();

    // Shift-tab wrap around to last element
    await user.tab({ shift: true });
    expect(btn2).toHaveFocus();
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <Drawer isOpen={true} onClose={() => {}} title="Accessible Drawer">
        <p>Drawer content body</p>
        <button type="button">Action</button>
      </Drawer>
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
