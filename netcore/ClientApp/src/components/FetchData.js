import React, { Component } from 'react';

export class FetchData extends Component {
  static displayName = FetchData.name;

  constructor(props) {
    super(props);
    this.state = { questions: [], loading: true };
  }

  componentDidMount() {
    this.populateQuestionData();
  }

  static renderQuestionsTable(questions) {
    return (
      <table className='table table-striped' aria-labelledby="tabelLabel">
        <thead>
          <tr>
            <th>Id</th>
            <th>Question Text</th>
            <th>Pub Date</th>
          </tr>
        </thead>
        <tbody>
          {questions.map(question =>
            <tr key={question.id}>
              <td>{question.id}</td>
              <td>{question.questionText}</td>
              <td>{question.pubDate}</td>
            </tr>
          )}
        </tbody>
      </table>
    );
  }

  render() {
    let contents = this.state.loading
      ? <p><em>Loading...</em></p>
        : FetchData.renderQuestionsTable(this.state.questions);

    return (
      <div>
        <h1 id="tabelLabel" >Received Questions</h1>
        <p>This component demonstrates fetching data from the server.</p>
        {contents}
      </div>
    );
  }

  async populateQuestionData() {
    const response = await fetch('question');
    const data = await response.json();
    this.setState({ questions: data, loading: false });
  }
}
