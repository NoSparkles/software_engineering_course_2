import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Home from './pages/home/Home';
import RockPaperScissorsGame from './pages/RockPaperScissorsGame/RockPaperScissorsGame';
import FourInARowGame from './pages/FourInARowGame/FourInARowGame';
import PairMatchingGame from './pages/PairMathingGame/PairMatchingGame';

function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/rock-paper-scissors" element={<RockPaperScissorsGame />} />
        <Route path="/four-in-a-row" element={<FourInARowGame />} />
        <Route path="/pair-matching" element={<PairMatchingGame />} />
      </Routes>
    </Router>
  );
}

export default App;
