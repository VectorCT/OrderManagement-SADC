import { Component, type ErrorInfo, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

export class RouteErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Route render failed', error, info);
  }

  render() {
    if (!this.state.error) return this.props.children;
    return (
      <main role="alert">
        <h1>Something went wrong</h1>
        <p>{this.state.error.message}</p>
        <button type="button" onClick={() => window.location.reload()}>
          Reload and recover
        </button>
      </main>
    );
  }
}
