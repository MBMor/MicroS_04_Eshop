import './App.css';
import { apiConfig } from './config/apiConfig';

function App() {
  return (
    <main className="app-shell">
      <section className="hero">
        <p className="eyebrow">Eshop Capstone</p>

        <h1>Microservices E-shop Frontend</h1>

        <p className="description">
          React frontend for a local microservices portfolio project.
          The frontend will communicate only through the API Gateway.
        </p>

        <dl className="metadata">
          <div>
            <dt>Frontend</dt>
            <dd>React + TypeScript + Vite</dd>
          </div>

          <div>
            <dt>API Gateway</dt>
            <dd>{apiConfig.baseUrl}</dd>
          </div>

          <div>
            <dt>Status</dt>
            <dd>Frontend skeleton is running</dd>
          </div>
        </dl>
      </section>
    </main>
  );
}

export default App;