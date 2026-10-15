import { useState } from 'react';
import {
  Button,
  Drawer,
  Modal,
  PasswordField,
  PlatformState,
  Skeleton,
  StatusBadge,
  StatusMessage,
  Stepper,
  TextField,
  ToastRegion,
  type ToastData,
} from './index';

export function SampleComposition() {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [activeStep, setActiveStep] = useState(1);
  const [toasts, setToasts] = useState<ToastData[]>([
    {
      id: 'toast-1',
      type: 'info',
      title: 'Update Available',
      description: 'A new version of the design system is active.',
      duration: 0,
    },
  ]);

  const handleDismissToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  return (
    <main
      id="main-sample-content"
      tabIndex={-1}
      className="mx-auto max-w-6xl space-y-10 px-4 py-8 focus:outline-none sm:px-6 lg:px-8"
    >
      <header className="border-b border-neutral-200 pb-4">
        <h1 className="text-2xl font-bold tracking-tight text-neutral-900">
          Design System Primitives & Tokens Sample Composition
        </h1>
        <p className="mt-1 text-sm text-neutral-600">
          Vitest-rendered sample composition validating accessibility, focus
          handling, and layout behavior.
        </p>
      </header>

      {/* Form Controls Section */}
      <section
        aria-labelledby="form-controls-heading"
        className="space-y-6 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="form-controls-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          Form Inputs & Validation
        </h2>

        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <TextField
            id="sample-username"
            label="Username"
            hint="Enter your unique system operator handle."
            placeholder="e.g. admin_operator"
            required
          />

          <TextField
            id="sample-email-error"
            label="Email Address"
            error="Please provide a valid corporate email address."
            defaultValue="invalid-email"
            required
          />

          <PasswordField
            id="sample-password"
            label="Master Key Password"
            hint="Minimum 16 characters including uppercase, numbers, and symbols."
            required
          />

          <PasswordField
            id="sample-password-error"
            label="Confirmation Password"
            error="Passwords do not match."
            defaultValue="secret123"
            required
          />

          <TextField
            id="sample-disabled"
            label="Cluster ID (Immutable)"
            defaultValue="cluster-prod-001"
            disabled
          />
        </div>
      </section>

      {/* Buttons Section */}
      <section
        aria-labelledby="buttons-heading"
        className="space-y-4 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="buttons-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          Button Variants & States
        </h2>

        <div className="flex flex-wrap items-center gap-3">
          <Button variant="primary">Primary Action</Button>
          <Button variant="secondary">Secondary Action</Button>
          <Button variant="outline">Outline Action</Button>
          <Button variant="danger">Danger Action</Button>
          <Button variant="ghost">Ghost Action</Button>
        </div>

        <div className="flex flex-wrap items-center gap-3 pt-2">
          <Button variant="primary" size="sm">
            Small
          </Button>
          <Button variant="primary" size="md">
            Medium
          </Button>
          <Button variant="primary" size="lg">
            Large
          </Button>
          <Button variant="primary" loading loadingText="Saving changes...">
            Submit
          </Button>
          <Button variant="secondary" disabled>
            Disabled
          </Button>
        </div>
      </section>

      {/* Status Badges & Messages Section */}
      <section
        aria-labelledby="status-heading"
        className="space-y-6 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="status-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          Status Indicators (Non-Color-Only)
        </h2>

        <div className="flex flex-wrap items-center gap-3">
          <StatusBadge severity="success">Operational</StatusBadge>
          <StatusBadge severity="warning">High Latency</StatusBadge>
          <StatusBadge severity="error">Offline</StatusBadge>
          <StatusBadge severity="info">Sync In Progress</StatusBadge>
          <StatusBadge severity="neutral">Standby</StatusBadge>
        </div>

        <div className="space-y-3">
          <StatusMessage
            severity="success"
            title="Cluster Verified"
            action={<Button size="sm">Continue</Button>}
          >
            All 3 nodes acknowledged the consensus protocol.
          </StatusMessage>

          <StatusMessage
            severity="warning"
            title="Memory Threshold Warning"
            action={
              <Button variant="outline" size="sm">
                View Metrics
              </Button>
            }
          >
            Memory utilization exceeds 85% on node-2.
          </StatusMessage>

          <StatusMessage
            severity="error"
            title="Connection Refused"
            action={
              <Button variant="danger" size="sm">
                Retry Connection
              </Button>
            }
          >
            Unable to reach vector database endpoint on port 6333.
          </StatusMessage>
        </div>
      </section>

      {/* Stepper Section */}
      <section
        aria-labelledby="stepper-heading"
        className="space-y-4 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="stepper-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          Setup Stepper
        </h2>

        <Stepper
          currentStepIndex={activeStep}
          onStepClick={(step) => setActiveStep(step)}
          steps={[
            {
              id: 'step-db',
              title: 'Database Migration',
              description: 'Schema setup and verification',
            },
            {
              id: 'step-keys',
              title: 'Root Keys',
              description: 'Master key generation & proof',
            },
            {
              id: 'step-admin',
              title: 'Superadmin Account',
              description: 'Create initial deployment owner',
            },
          ]}
        />
      </section>

      {/* Modals & Drawers Section */}
      <section
        aria-labelledby="overlays-heading"
        className="space-y-4 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="overlays-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          Accessible Dialogs & Overlays
        </h2>

        <div className="flex flex-wrap gap-4">
          <Button
            id="open-modal-trigger"
            onClick={() => setIsModalOpen(true)}
            variant="outline"
          >
            Open Configuration Modal
          </Button>

          <Button
            id="open-drawer-trigger"
            onClick={() => setIsDrawerOpen(true)}
            variant="outline"
          >
            Open Audit Drawer
          </Button>
        </div>

        <Modal
          isOpen={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          title="Security Preferences"
          description="Update global encryption key rotation thresholds."
        >
          <div className="space-y-4">
            <TextField
              id="modal-rotation-days"
              label="Rotation Frequency (Days)"
              defaultValue="90"
            />
            <div className="flex justify-end gap-3 pt-2">
              <Button variant="ghost" onClick={() => setIsModalOpen(false)}>
                Cancel
              </Button>
              <Button variant="primary" onClick={() => setIsModalOpen(false)}>
                Apply Changes
              </Button>
            </div>
          </div>
        </Modal>

        <Drawer
          isOpen={isDrawerOpen}
          onClose={() => setIsDrawerOpen(false)}
          title="System Audit Log"
          description="Recent critical events logged across deployment nodes."
        >
          <div className="space-y-3">
            <p className="text-sm text-neutral-600">
              No audit violations reported in the last 24 hours.
            </p>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setIsDrawerOpen(false)}
            >
              Close Drawer
            </Button>
          </div>
        </Drawer>
      </section>

      {/* Skeleton Loading Section */}
      <section
        aria-labelledby="skeletons-heading"
        className="space-y-4 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="skeletons-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          Loading Skeletons (Screen-Reader Announced)
        </h2>

        <div className="flex items-center gap-4">
          <Skeleton circle width="48px" height="48px" />
          <div className="flex-1 space-y-2">
            <Skeleton height="16px" width="60%" />
            <Skeleton height="14px" width="90%" />
          </div>
        </div>
      </section>

      {/* Canonical Platform States Section */}
      <section
        aria-labelledby="states-heading"
        className="space-y-6 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <div className="border-b border-neutral-200 pb-3">
          <h2
            id="states-heading"
            className="text-lg font-semibold text-neutral-900"
          >
            Canonical Platform States
          </h2>
          <p className="mt-1 text-sm text-neutral-600">
            Validated visual and interaction baseline for error, loading,
            forbidden, empty, expired session, recovery pending, and success
            states.
          </p>
        </div>

        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <PlatformState
            type="error"
            title="Database Connection Lost"
            description="The primary PostgreSQL cluster failed health checks on port 5432."
            details="Npgsql.NpgsqlException: Connection refused (0x0000274D) host 'postgres:5432'"
            action={{
              label: 'Retry Connection',
              onClick: () => {},
            }}
          />

          <PlatformState
            type="loading"
            title="Syncing Index Generation..."
            description="Calculating vector projections and validating chunk offsets for workspace collections."
          />

          <PlatformState
            type="forbidden"
            title="Tenant Isolation Restricted"
            description="You do not have the required TenantAdmin role to modify key protector settings or view deployment audit logs."
            action={{
              label: 'Return to Dashboard',
              onClick: () => {},
            }}
          />

          <PlatformState
            type="empty"
            title="No Documents Ingested"
            description="Upload PDF, Markdown, or HTML files to begin generating embeddings and visual citations."
            action={{
              label: 'Upload Document',
              onClick: () => {},
            }}
          />

          <PlatformState
            type="expired-session"
            title="Session Expired"
            description="Your authenticated session has expired due to inactivity. Re-authenticate to resume."
            action={{
              label: 'Sign In Again',
              onClick: () => {},
            }}
          />

          <PlatformState
            type="recovery-pending"
            title="Master Key Recovery Required"
            description="The key protector is locked following a hardware platform change. Supply the recovery envelope to proceed."
            details="STATUS: LOCKED/RECOVERY_REQUIRED | Protector: tpm-cng-01"
            action={{
              label: 'Enter Recovery Kit',
              onClick: () => {},
            }}
          />

          <div className="md:col-span-2">
            <PlatformState
              type="success"
              title="Key Protector Active & Verified"
              description="Master encryption key initialized. Offline recovery envelope exported and verified successfully."
              action={{
                label: 'Proceed to Setup Center',
                onClick: () => {},
              }}
            />
          </div>
        </div>
      </section>

      {/* Toast Region */}
      <ToastRegion toasts={toasts} onDismiss={handleDismissToast} />
    </main>
  );
}
