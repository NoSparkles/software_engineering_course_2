import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Home from './pages/home/Home';
import RockPaperScissorsGame from './pages/RockPaperScissorsGame/RockPaperScissorsGame';
import FourInARowGame from './pages/FourInARowGame/FourInARowGame';
import PairMatchingGame from './pages/PairMathingGame/PairMatchingGame';
import GameEntry from './components/GameEntry';
import WaitingRoom from './pages/WaitingRoom/WaitingRoom';

function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/rock-paper-scissors" element={<GameEntry />} />
        <Route path="/four-in-a-row" element={<GameEntry />} />
        <Route path="/pair-matching" element={<GameEntry />} />
        <Route path="/rock-paper-scissors/session/:code" element={<RockPaperScissorsGame />} />
        <Route path="/four-in-a-row/session/:code" element={<FourInARowGame />} />
        <Route path="/pair-matching/session/:code" element={<PairMatchingGame />} />
        <Route path="/:gameType/waiting/:code" element={<WaitingRoom />} />
      </Routes>
    </Router>
  );
}

export default App;
